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

namespace HoustonRadarCSharpAppEx
{
    class Program
    {
        private static int[] radarIPs = new int[20];
        private static DateTime start = new DateTime();
        private static DateTime end = new DateTime();
        private static string gpsLocation = "";

        static void Main(string[] args)
        {
            Console.WriteLine("Program starting...");

            for (int i = 10; i < radarIPs.Length; i++)
            {
                radarIPs[i] = 40 + i;
                gpsLocation = "";
                var curRadar = new radarCommClassThd(null);
                ConnectToRadar(curRadar, radarIPs[i]);
            }

            Console.WriteLine("Waiting for all radar connections...");
            Console.WriteLine("All radars connected or failed. Program will now exit.");
        }

        private static void ConnectToRadar(radarCommClassThd rdr, int ip)
        {
            ManualResetEvent radarReady = new ManualResetEvent(false);

            rdr.IPaddr = "161.184.106." + ip.ToString();
            rdr.portnum = 5125;
            rdr.DoSerialConnect = false;
            rdr.ReadTimeout = 2000;
            rdr.WriteTimeout = 2000;

            rdr.RadarEventRadarFound += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} was successfully pinged!");
            };

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
                            gpsLocation += $"Latitude: {parts[0]}";
                            gpsLocation += $"\nLongitude: {parts[1]}";
                        }
                        break;
                    }
                }

                ConnectToAPI(ip);
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

        static void ConnectToAPI(int ip)
        {
            string url = $"http://api.spectrumtraffic.com/radar.php?act=get_schedules&ip_address=161.184.106.{ip}";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = reader.ReadToEnd();
                    JObject json = JObject.Parse(jsonResponse);

                    if (json["schedules"] != null && json["schedules"].HasValues)
                    {
                        string fakeStart = json["schedules"][0]["start"].ToString();
                        string fakeEnd = json["schedules"][0]["end"].ToString();

                        start = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeStart)).LocalDateTime;
                        end = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeEnd)).LocalDateTime;

                        Console.WriteLine($"Start: {start}");
                        Console.WriteLine($"End: {end}");
                    }
                    else
                    {
                        Console.WriteLine($"Radar {ip}: No schedules found.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Radar {ip} API error: {ex.Message}");
            }
        }

        private static void readData(radarCommClassThd rdr, int ip)
        {
            Console.WriteLine($"Entered readData function for radar {ip}...");

            speedLanePreformedQueries schema = new speedLanePreformedQueries(rdr);
            schema.ReadVehiclesDateRange(start, end);
            schema.progressevent += schema_progressevent;
            schema.progressPctComplete += schema_progressPctComplete;

            var vehicles = schema.getSpeedLaneVehicles();
            if (vehicles == null || vehicles.Length == 0)
            {
                Console.WriteLine($"No vehicles found for radar {ip}.");
            }
            else
            {
                parseAndPrintVehicles("km/h", "m", vehicles, ip);
            }
        }

        private static void schema_progressevent(int packetno, int bytes)
        {
            Console.WriteLine($"Packet #{packetno}, bytes: {bytes}");
        }

        private static void schema_progressPctComplete(int pct)
        {
            Console.WriteLine($"Progress: {pct}%");
        }

        private static void parseAndPrintVehicles(
            string speedUnit,
            string lengthUnit,
            speedLanePreformedQueries.speedLaneVehicleRec[] vehicles,
            int ip)
        {
            Console.WriteLine($"Entered parseAndPrintVehicles for radar {ip}.");

            string jsonFileName = $"{DateTime.Now:yyyyMMdd-HHmmss}_{ip}.json";
            string compressedFileName = $"{jsonFileName}.gz";

            using (var sw = new StreamWriter(jsonFileName))
            {
                sw.WriteLine($"Ip address: 161.184.106.{ip}");
                sw.WriteLine($"Number of vehicles: {vehicles.Length}");
                sw.WriteLine(gpsLocation);
                sw.WriteLine("[");
                foreach (var rec in vehicles)
                {
                    sw.WriteLine(
                        "\t{" +
                        $"\n\t\tTimeEnding: {rec._dt}," +
                        $"\n\t\tLane: {rec._lane}," +
                        $"\n\t\tSpeed: {rec._speed} {speedUnit}," +
                        $"\n\t\tLength: {Math.Round(rec._length) / 100.0} {lengthUnit}," +
                        $"\n\t\tDirection: {rec._direction}" +
                        "\n\t},"
                    );
                }
                sw.WriteLine("]");
            }

            Console.WriteLine($"JSON file created: {jsonFileName}");

            // compress json file
            CompressJsonFile(jsonFileName, compressedFileName);
            Console.WriteLine($"Compressed file created: {compressedFileName}");
        }

        private static void CompressJsonFile(string inputFile, string outputFile)
        {
            using (FileStream inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
            using (FileStream outputStream = new FileStream(outputFile, FileMode.Create))
            using (GZipStream gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                inputStream.CopyTo(gzipStream);
            }
        }
    }
}
