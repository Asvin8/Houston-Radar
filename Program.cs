using System;
using System.Threading;
using HoustonRadarLLC.StatsAnalyzer;
using HoustonRadarLLC.Models.Comm;
using HoustonRadarLLC.DAL.Comm;
using HoustonRadarLLC.Models;
using System.Collections.Generic;
using HoustonRadarLLC.StatsAnalyzer.Data;
using System.IO;
using Newtonsoft.Json;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using HoustonRadarLLC.DAL.Comm;
using HoustonRadarLLC.Models.Comm;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HoustonRadarCSharpAppEx
{
    class Program
    {
        private static radar_generic genericRdr;
        static ManualResetEvent pauseEvent = new ManualResetEvent(false);
        //private static radarCommClassThd rdr = new radarCommClassThd(null);
        private static int totalbytes = 0;
        private static int totalpackets = 0;

        private static DateTime start = new DateTime();
        private static DateTime end = new DateTime();

        private static int[] radarIPs = new int[20];
        private static radarCommClassThd curRadar;

        static void Main(string[] args)
        {
            Console.WriteLine("Connection starting...");

            for (int i = 0; i < radarIPs.Length; i++)
            {
                radarIPs[i] = 40 + i;
                curRadar = new radarCommClassThd(null);
                ConnectToRadar(curRadar, radarIPs[i]);
            }

            pauseEvent.WaitOne();
            Console.WriteLine("Connection completed.");

        }

        private static void ConnectToRadar(radarCommClassThd rdr, int ip)
        {
            rdr.IPaddr = "161.184.106." + ip.ToString();
            rdr.portnum = 5125;
            rdr.DoSerialConnect = false;
            rdr.ReadTimeout = 2000;
            rdr.WriteTimeout = 2000;

            rdr.RadarEventRadarFound += rdr_RadarEventRadarFound;

            // Execute readData() when connection setup completes
            rdr.RadarEventGetInfoDone += (sender, e) =>
            {
                Console.WriteLine("Radar connection established. Now reading data from radar " + rdr.IPaddr);
                ConnectToAPI(ip);
                readData(rdr, ip);
            };

            rdr.RadarEventRadarNotFound += rdr_RadarEventRadarNotFound;
            rdr.RadarEventEx += rdr_RadarEventEx;
            rdr.RadarEventCommErr += rdr_RadarEventCommErr;
            rdr.RadarEventOOBdata += rdr_RadarEventOOBdata;

            rdr.Connect();
        }

        static void ConnectToAPI(int ip)
        {
            string url = "https://api.spectrumtraffic.com/radar.php?act=get_schedules&ip_address=161.184.106." + ip.ToString();

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Synchronously fetch response
                    HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode(); // Throws exception if status is not 200-299

                    // Synchronously read the response content
                    string jsonResponse = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    // Parse JSON response
                    JObject json = JObject.Parse(jsonResponse);

                    // Access the "start" and "end" elements
                    if (json["schedules"] != null && json["schedules"].HasValues)
                    {
                        string fakeStart = json["schedules"][0]["start"].ToString();
                        string fakeEnd = json["schedules"][0]["end"].ToString();

                        //start = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeStart)).LocalDateTime;
                        //end = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fakeEnd)).LocalDateTime;

                        start = new DateTime(2024, 05, 07, 00, 00, 00);
                        end = new DateTime(2024, 05, 07, 23, 59, 59);

                        Console.WriteLine($"Start: {start}");
                        Console.WriteLine($"End: {end}");
                    }
                    else { Console.WriteLine("Schedules not found in the response."); }
                }
                catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
            }
            //Thread.Sleep(9000);
        }

        private static void readData(radarCommClassThd rdr, int ip)
        {

            Console.WriteLine("entered readData function for " + ip + "!!!!!");

            HoustonRadarLLC.speedLanePreformedQueries schema = new HoustonRadarLLC.speedLanePreformedQueries(rdr);
            schema.ReadVehiclesDateRange(start, end);

            schema.progressevent += new HoustonRadarLLC.speedLanePreformedQueries.progressdelegate(schema_progressevent);
            schema.progressPctComplete += new HoustonRadarLLC.speedLaneSchema.progressPctCompleteDelegate(schema_progressPctComplete);

            string speedUnit = "km/h";
            string lengthUnit = "m";

            // Look at individual vehicles and print date/timestamp, lane, speed, length
            HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec[] vehicles = schema.getSpeedLaneVehicles();
            parseAndPrintVehicles(speedUnit, lengthUnit, vehicles, ip);
        }

        private static void schema_progressevent(int packetno, int bytes)
        {
            totalbytes += bytes;
            totalpackets++;
            Console.WriteLine("Pkt #" + totalpackets + " (Total:" + ((float)totalbytes / 1024.0).ToString("0.0") + "kB)");
        }

        private static void schema_progressPctComplete(int pct) { Console.WriteLine(pct.ToString()); }

        private static void parseAndPrintVehicles(string speedUnit, string lengthUnit, HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec[] vehicles, int ip)
        {
            Console.WriteLine("entered parseAndPrintVehicles function for " + ip + "!!!!!");
            using (StreamWriter sw = new StreamWriter(DateTime.Now.ToString("yyyyMMdd-hhmmsstt") + ".json"))
            {
                sw.WriteLine("Ip address: 161.184.106." + ip);
                sw.WriteLine("Number of vehicles: " + vehicles.Length);
                sw.WriteLine("[");
                foreach (HoustonRadarLLC.speedLanePreformedQueries.speedLaneVehicleRec rec in vehicles)
                {
                    sw.WriteLine(
                        "\t{" +
                        "\n\t\tTimeEnding: " + rec._dt.ToString() +
                        "\n\t\tLane: " + rec._lane +
                        "\n\t\tSpeed: " + rec._speed.ToString() + " " + speedUnit +
                        "\n\t\tLength: " + (Math.Round(rec._length) / 100.0).ToString() + " " + lengthUnit +
                        "\n\t\tDirection: " + rec._direction.ToString() +
                        "\n\t},"
                    );
                }
                sw.WriteLine("]");
            }
            Console.WriteLine("exited parseAndPrintVehicles function for " + ip);
        }


        private static void rdr_RadarEventRadarFound(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Radar was successfully pinged!");
        }

        private static void rdr_RadarEventGetInfoDone(object sender, RadarCommEventArgs e)
        {

            genericRdr = (new radar_speedlane(curRadar)) as radar_speedlane;
            genericRdr.progressupdate += new progressupdatehandler(varRead_progressupdate);

            string err;
            if (!genericRdr.filldata(out err))
                Console.WriteLine("Error: " + err);

            genericRdr.progressupdate -= new progressupdatehandler(varRead_progressupdate);
            Console.WriteLine("Radar system data has been successfully recieved.");

            radar_generic.RadarConfig radarCfg = genericRdr.GetAllConfig();

        }

        private static void varRead_progressupdate(int pct, string var, string val, progressenum state)
        {
            if (state == progressenum.stop)
            {
                Console.WriteLine("Progress update complete");

                if (!pauseEvent.WaitOne(0))
                    pauseEvent.Set();
            }
        }

        private static void rdr_RadarEventRadarNotFound(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Radar not found.");
            pauseEvent.Set();
        }

        private static void rdr_RadarEventCommErr(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Communication error: " + e.CommErrStr);
        }

        private static void rdr_RadarEventEx(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Exception: " + e.ex.Message);
        }

        private static void rdr_RadarEventOOBdata(object sender, RadarCommEventArgs e)
        {
            Console.WriteLine("Out of Band Data: " + e.oobdata);
        }
    }
}
