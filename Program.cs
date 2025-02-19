using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;
using HoustonRadarLLC.Models.Comm;
using HoustonRadarLLC.StatsAnalyzer;
using HoustonRadarLLC.StatsAnalyzer.Data;
using HoustonRadarLLC.DAL.Comm;

namespace HoustonRadarCSharpAppEx
{
    class Program
    {
        private static radar_generic genericRdr;
        private static int totalbytes = 0;
        private static int totalpackets = 0;
        private static DateTime start = new DateTime();
        private static DateTime end = new DateTime();
        private static int[] radarIPs = new int[20];
        private static CountdownEvent countdown;

        static void Main(string[] args)
        {
            Console.WriteLine("Connection starting...");

            countdown = new CountdownEvent(radarIPs.Length); // Initialize countdown for radars

            for (int i = 0; i < radarIPs.Length; i++)
            {
                radarIPs[i] = 40 + i;
                var curRadar = new radarCommClassThd(null);
                ConnectToRadar(curRadar, radarIPs[i]);
            }

            Console.WriteLine("Waiting for all radar connections...");
            countdown.Wait(); // Block until all radars are processed

            Console.WriteLine("All radars connected. Program will now exit.");

            Thread.Sleep(9000);
        }

        private static void ConnectToRadar(radarCommClassThd rdr, int ip)
        {
            rdr.IPaddr = "161.184.106." + ip.ToString();
            rdr.portnum = 5125;
            rdr.DoSerialConnect = false;
            rdr.ReadTimeout = 2000;
            rdr.WriteTimeout = 2000;

            rdr.RadarEventRadarFound += rdr_RadarEventRadarFound;

            // Handle successful connection
            rdr.RadarEventGetInfoDone += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} connected. Fetching data...");
                ConnectToAPI(ip);
                readData(rdr, ip);
                countdown.Signal(); // Mark radar as completed
            };

            // Handle failed connection
            rdr.RadarEventRadarNotFound += (sender, e) =>
            {
                Console.WriteLine($"Radar {ip} not found.");
                countdown.Signal();
            };

            rdr.RadarEventCommErr += (sender, e) =>
            {
                Console.WriteLine($"Communication error: {e.CommErrStr}");
                countdown.Signal();
            };

            rdr.Connect();
        }

        static void ConnectToAPI(int ip)
        {
            string url = $"https://api.spectrumtraffic.com/radar.php?act=get_schedules&ip_address=161.184.106.{ip}";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    string jsonResponse = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

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
                        Console.WriteLine("Schedules not found in the response.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

        private static void readData(radarCommClassThd rdr, int ip)
        {
            Console.WriteLine($"Entered readData function for {ip}!!!!!");

            if (rdr == null)
            {
                Console.WriteLine($"Error: Radar object is null for IP {ip}. Skipping readData.");
                return;
            }

            try
            {
                HoustonRadarLLC.speedLanePreformedQueries schema = new HoustonRadarLLC.speedLanePreformedQueries(rdr);

                if (schema == null)
                {
                    Console.WriteLine($"Error: Failed to initialize schema for {ip}.");
                    return;
                }

                schema.ReadVehiclesDateRange(start, end);

                schema.progressevent += new HoustonRadarLLC.speedLanePreformedQueries.progressdelegate(schema_progressevent);
                schema.progressPctComplete += new HoustonRadarLLC.speedLaneSchema.progressPctCompleteDelegate(schema_progressPctComplete);

                string speedUnit = "km/h";
                string lengthUnit = "m";

                HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec[] vehicles = schema.getSpeedLaneVehicles();

                if (vehicles == null || vehicles.Length == 0)
                {
                    Console.WriteLine($"Warning: No vehicles found for radar {ip}.");
                }
                else
                {
                    parseAndPrintVehicles(speedUnit, lengthUnit, vehicles, ip);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in readData for {ip}: {ex.Message}");
            }
        }

        private static void schema_progressevent(int packetno, int bytes)
        {
            totalbytes += bytes;
            totalpackets++;
            Console.WriteLine($"Pkt #{totalpackets} (Total: {(float)totalbytes / 1024.0:0.0} kB)");
        }

        private static void schema_progressPctComplete(int pct)
        {
            Console.WriteLine(pct.ToString());
        }

        private static void parseAndPrintVehicles(string speedUnit, string lengthUnit, HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec[] vehicles, int ip)
        {
            Console.WriteLine($"Entered parseAndPrintVehicles function for {ip}!!!!!");

            using (StreamWriter sw = new StreamWriter($"{DateTime.Now:yyyyMMdd-hhmmsstt}.json"))
            {
                sw.WriteLine($"Ip address: 161.184.106.{ip}");
                sw.WriteLine($"Number of vehicles: {vehicles.Length}");
                sw.WriteLine("[");

                foreach (HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec rec in vehicles)
                {
                    sw.WriteLine(
                        "\t{" +
                        $"\n\t\tTimeEnding: {rec._dt}" +
                        $"\n\t\tLane: {rec._lane}" +
                        $"\n\t\tSpeed: {rec._speed} {speedUnit}" +
                        $"\n\t\tLength: {(Math.Round(rec._length) / 100.0)} {lengthUnit}" +
                        $"\n\t\tDirection: {rec._direction}" +
                        "\n\t},"
                    );
                }
                sw.WriteLine("]");
            }
            Console.WriteLine($"Exited parseAndPrintVehicles function for {ip}");
        }

        private static void rdr_RadarEventRadarFound(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Radar was successfully pinged!");
        }

        private static void rdr_RadarEventCommErr(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine($"Communication error: {e.CommErrStr}");
        }

        private static void rdr_RadarEventEx(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine($"Exception: {e.ex.Message}");
        }

        private static void rdr_RadarEventOOBdata(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine($"Out of Band Data: {e.oobdata}");
        }
    }
}
