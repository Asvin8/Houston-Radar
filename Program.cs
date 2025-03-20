using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HoustonRadarLLC.Models.Comm;
using HoustonRadarLLC.StatsAnalyzer;
using HoustonRadarLLC.StatsAnalyzer.Data;
using HoustonRadarLLC.DAL.Comm;
using HoustonRadarLLC;
using System.IO;
using System.Net;
using System.IO.Compression;
using dotnetCHARTING.WinForms;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization;
 
 
namespace HoustonRadarCSharpAppEx
{
    class Program
    {
        private static DateTime start = new DateTime();
        private static DateTime end = new DateTime();
        private static radarCommClassThd rdr = new radarCommClassThd(null);
        private static int ip = -1; 
        private static double latitude = -1;
        private static double longitude = -1;
        private static string equipmentGuid = "";
        private static string serialNo = ""; 
 
        static void Main(string[] args)
        {
            Console.WriteLine("New and updated..........");
            Console.WriteLine("Program starting...");
 
            end = DateTime.Now;
            start = end.AddMinutes(-35);
 
            Console.WriteLine("Starting time: " + start);
            Console.WriteLine("Ending time: " + end);
 
            ReadScheduleAPI(); 
 
            Console.WriteLine("Waiting for all radar connections...");
            Console.WriteLine("All radars connected or failed. Program will now exit.");
 
            Thread.Sleep(90000);
        }
 
        private static void ConnectToRadar()
        {
            ManualResetEvent radarReady = new ManualResetEvent(false);
 
            rdr.IPaddr = "161.184.106." + ip.ToString();
            rdr.portnum = 5125;
            rdr.DoSerialConnect = false;
            rdr.ReadTimeout = 2000;
            rdr.WriteTimeout = 2000;
 
            rdr.RadarEventRadarFound += (sender, e) => { Console.WriteLine($"Radar {ip} was successfully pinged!");  };
 
            rdr.RadarEventGetInfoDone += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} connected. Now reading data...");
 
                string[] lines = e.getinfostr.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.StartsWith("POS="))
                    {
                        string coordinates = line.Substring(4);
                        string[] parts = coordinates.Split(',');
 
                        if (parts.Length == 2)
                        {
                            latitude = double.Parse(parts[0]);
                            longitude = double.Parse(parts[1]);
                        }
                        break;
                    }
                }
 
                //ReadScheduleAPI(ip);
                readData(rdr, ip);
                radarReady.Set();
            };
 
            rdr.RadarEventRadarNotFound += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} not found.");
                radarReady.Set();
            };
 
            rdr.RadarEventCommErr += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} communication error: {e.CommErrStr}");
                radarReady.Set();
            };
 
            rdr.RadarEventEx += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} exception: {e.ex.Message}");
                radarReady.Set();
            };
 
            rdr.Connect();
            radarReady.WaitOne();
        }
 
        private static void ReadScheduleAPI()
        {
            string url = "http://api.spectrumtraffic.com/radar.php?act=get_active_radars";
 
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
 
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = reader.ReadToEnd();
                    JObject data = JObject.Parse(jsonResponse);
 
                    if (data["err"] != null && data["err"].ToString() == "0")
                    {
                        foreach (var radar in data["radars"])
                        {
 
                            rdr = new radarCommClassThd(null);
 
                            equipmentGuid = radar["equipment_guid"].ToString();
                            serialNo = radar["serial_no"].ToString();
 
                            string ipAddress = radar["ip_address"].ToString();
                            ip = int.Parse(ipAddress.Split('.')[3]);
 
                            //string fakeStart = radar["start_time"].ToString();
                            //string fakeEnd = radar["end_time"].ToString();
                            //start = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeStart)).LocalDateTime;
                            //end = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeEnd)).LocalDateTime;
 
                            Console.WriteLine("----------------------");
                            Console.WriteLine($"Equipment GUID: {equipmentGuid}");
                            Console.WriteLine($"IP Address: {ipAddress}");
                            Console.WriteLine("----------------------");
 
                            ConnectToRadar();
 
                        }
                    }
                    else { Console.WriteLine("Error retrieving data from API."); }
                }
            }
            catch (Exception ex) { Console.WriteLine("Exception: " + ex.Message); }
        }
 
        private static void readData(radarCommClassThd rdr, int ip)
        {
            Console.WriteLine($"Entered readData function for radar {ip}...");
 
            speedLanePreformedQueries schema = new speedLanePreformedQueries(rdr);
            schema.ReadVehiclesDateRange(start, end);
            schema.progressevent += schema_progressevent;
            schema.progressPctComplete += schema_progressPctComplete;
            var vehicles = schema.getSpeedLaneVehicles();
 
            if (vehicles == null || vehicles.Length == 0) { Console.WriteLine($"No vehicles found for radar {ip}."); }
            else { parseAndPrintVehicles(vehicles); }
        }
 
        private static void schema_progressevent(int packetno, int bytes) { Console.WriteLine($"Packet #{packetno}, bytes: {bytes}"); }
 
        private static void schema_progressPctComplete(int pct) { Console.WriteLine($"Progress: {pct}%"); }
 
        private static void parseAndPrintVehicles(speedLanePreformedQueries.speedLaneVehicleRec[] vehicles)
        {
            Console.WriteLine($"Entered parseAndPrintVehicles for radar {ip}.");
 
            // Define CSV filename
            string csvFileName = $"{DateTime.Now:yyyyMMdd-HHmmss}_{ip}.csv";
            string compressedFileName = $"{csvFileName}.gz";
 
            // Write CSV data
            using (var writer = new StreamWriter(csvFileName))
            {
                writer.WriteLine("id,time,lane,speed_kmh,length_cm,direction,latitude,longitude,equipment_guid");
 
                // Write each vehicle record
                if (vehicles != null)
                {
                    foreach (var rec in vehicles)
                    {
                        string idToPrint = serialNo + "-" + rec._uid.ToString("D10");
                        writer.WriteLine($"{idToPrint},{rec._dt.ToString("o", CultureInfo.InvariantCulture)},{rec._lane}," +
                            $"{rec._speed},{rec._length},{rec._direction},{latitude},{longitude},{equipmentGuid}");
                    }
                }
            }
 
            Console.WriteLine($"CSV file created: {csvFileName}");
 
            // Compress and upload CSV file
            ProcessCsvFile(csvFileName, compressedFileName);
            Console.WriteLine($"Compressed file created: {compressedFileName}");
        }
 
        private static void ProcessCsvFile(string inputFile, string outputFile)
        {
            // Compress CSV file
            using (FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            using (FileStream outputStream = new FileStream(outputFile, FileMode.Create))
            using (GZipStream gzipStream = new GZipStream(outputStream, CompressionMode.Compress)) { inputStream.CopyTo(gzipStream); }
 
            // Define boundary for multipart/form-data
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            string url = "http://api.spectrumtraffic.com/radar.php?act=upload_gzip";
 
            // Create HttpWebRequest
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
 
            using (Stream requestStream = request.GetRequestStream())
            using (BinaryWriter writer = new BinaryWriter(requestStream))
            {
                // Start boundary
                string boundaryStart = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{Path.GetFileName(outputFile)}\"\r\nContent-Type: application/gzip\r\n\r\n";
                byte[] boundaryStartBytes = System.Text.Encoding.UTF8.GetBytes(boundaryStart);
                writer.Write(boundaryStartBytes);
 
                // Write file content
                byte[] fileData = File.ReadAllBytes(outputFile);
                writer.Write(fileData);
 
                // End boundary
                string boundaryEnd = $"\r\n--{boundary}--\r\n";
                byte[] boundaryEndBytes = System.Text.Encoding.UTF8.GetBytes(boundaryEnd);
                writer.Write(boundaryEndBytes);
            }
 
            File.Delete(inputFile); 
            File.Delete(outputFile);
 
            // Get and process the response (CSV, not GZIP)
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = reader.ReadToEnd();
                    Console.WriteLine("Response: " + responseText);
                }
            }
            catch (WebException ex)
            {
                using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string errorText = reader.ReadToEnd();
                    Console.WriteLine("Error: " + errorText);
                }
            }
        }
 
    }
}
