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

namespace HoustonRadarCSharpAppEx
{
    class Program
    {
        private static int[] radarIPs = new int[20];
        private static CountdownEvent countdown; // Tracks how many radars have finished
        private static DateTime start = new DateTime();
        private static DateTime end = new DateTime();

        static void Main(string[] args)
        {
            Console.WriteLine("Connection starting...");

            // Initialize the CountdownEvent to the total number of radars
            //countdown = new CountdownEvent(radarIPs.Length);

            // Create tasks for each radar connection
            //List<Task> radarTasks = new List<Task>();
            for (int i = 0; i < radarIPs.Length; i++)
            {
                radarIPs[i] = 40 + i;
                var curRadar = new radarCommClassThd(null);

                // Run each connection on a background thread
                ConnectToRadar(curRadar, radarIPs[i]);
            }

            Console.WriteLine("Waiting for all radar connections...");

            // Wait until all radars are finished (success or fail)
            //countdown.Wait();

            Console.WriteLine("All radars connected or failed. Program will now exit.");
        }

        private static void ConnectToRadar(radarCommClassThd rdr, int ip)
        {
            ManualResetEvent radarReady = new ManualResetEvent(false); // Block until radar connects

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
                ConnectToAPI(ip);
                readData(rdr, ip);
                //countdown.Signal();
                radarReady.Set();  // Unblock execution
            };

            rdr.RadarEventRadarNotFound += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} not found.");
                //countdown.Signal();
                radarReady.Set();  // Unblock execution
            };

            rdr.RadarEventCommErr += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} communication error: {e.CommErrStr}");
                //countdown.Signal();
                radarReady.Set();  // Unblock execution
            };

            rdr.RadarEventEx += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} exception: {e.ex.Message}");
                //countdown.Signal();
                radarReady.Set();  // Unblock execution
            };

            rdr.Connect();

            // Block execution here until the radar event fires
            radarReady.WaitOne();
        }


        static void ConnectToAPI(int ip)
        {
            string url = $"https://api.spectrumtraffic.com/radar.php?act=get_schedules&ip_address=161.184.106.{ip}";

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";  // Explicitly use GET method

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string jsonResponse = reader.ReadToEnd();

                    // Parse JSON response
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

            // Example library usage
            speedLanePreformedQueries schema = new speedLanePreformedQueries(rdr);
            schema.ReadVehiclesDateRange(start, end);

            schema.progressevent += schema_progressevent;
            schema.progressPctComplete += schema_progressPctComplete;

            // Get the vehicles
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

            using (var sw = new StreamWriter($"{DateTime.Now:yyyyMMdd-HHmmss}_{ip}.json"))
            {
                sw.WriteLine($"Ip address: 161.184.106.{ip}");
                sw.WriteLine($"Number of vehicles: {vehicles.Length}");
                sw.WriteLine("[");
                foreach (var rec in vehicles)
                {
                    sw.WriteLine(
                        "\t{" +
                        $"\n\t\tTimeEnding: {rec._dt}" +
                        $"\n\t\tLane: {rec._lane}" +
                        $"\n\t\tSpeed: {rec._speed} {speedUnit}" +
                        $"\n\t\tLength: {Math.Round(rec._length) / 100.0} {lengthUnit}" +
                        $"\n\t\tDirection: {rec._direction}" +
                        "\n\t},"
                    );
                }
                sw.WriteLine("]");
            }

            Console.WriteLine($"JSON file created for radar {ip}.");
        }
    }
}
