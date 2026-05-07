using System;
using System.Numerics;
using System.Threading.Tasks;
using ThingMagic;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal class Program
{
    public class TagData
    {
        public TagData(string name, DateTime timestamp) {
            this.name = name;
            this.timestamp = timestamp;
        }

        public string name { get; set; }
        public DateTime timestamp { get; set; }
    }

    private static async Task Main(string[] args)
    {
        /*****************
         *      Setup
         *****************/

        Reader r = Reader.Create("tmr:///com4");
        //r.Transport += r.SimpleTransportListener; // DEBUG

        r.Connect();
        Console.WriteLine("Connected. Configuring...");

        r.ParamSet("/reader/region/id", Reader.Region.NA);
        r.ParamSet("/reader/radio/readPower", 2500);

        // S0 ensures tags keep responding even if they were just read
        r.ParamSet("/reader/gen2/session", Gen2.Session.S0);

        int[] antennaList = { 1 };
        SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
        r.ParamSet("/reader/read/plan", plan);


        /*****************
         *     Reading
         *****************/


        int check_every_seconds = 5; // Check if there are missing tags once every this many seconds
        DateTime next_check = DateTime.Now;
        Dictionary<string, TagData> trackedtags = new Dictionary<string, TagData>();
        HashSet<string> seentags = new HashSet<string>();
        Queue<string> newtags = new Queue<string>();

        r.TagRead += (sender, e) =>
        {
            newtags.Clear();
            string epc = e.TagReadData.EpcString;
            seentags.Add(epc);
            if (trackedtags.ContainsKey(epc)) {
                //Console.WriteLine("tag " + trackedtags[epc].name + " has associated timestamp: " + trackedtags[epc].timestamp.ToString());
                trackedtags[epc].timestamp = DateTime.Now;
            } else {
                newtags.Enqueue(epc);
            }
        };

        bool writeMenu = false;


        r.StartReading();

        string command = "";
        while (command != "q")
        {
            if (writeMenu)
            {
                Console.WriteLine("Reading Started... ");
                Console.Write("Choose an action:\n" +
                              "=========================================\n" +
                              "  - Press 's' for status of all tags\n" +
                              "  - Press 'n' to track a new tag\n" +
                              "  - Press 'l' to list all tracked tags\n" +
                              "  - Press 'q' to quit\n" +
                              "=========================================\n" +
                              " > ");
                writeMenu = false;
            }

            switch(command)
            {
                case "s":
                    r.StopReading();
                    int entrycount = 1;
                    Console.WriteLine();
                    Console.WriteLine("List of tags:");
                    foreach (KeyValuePair<string, TagData> entry in trackedtags)
                    {
                        string status = "Not found";
                        if (seentags.Contains(entry.Key))
                        {
                            status = "Found";
                        }
                        TimeSpan timeDifference = DateTime.Now.Subtract(entry.Value.timestamp);
                        Console.WriteLine(entrycount + ". Name: " + entry.Value.name + ":  ID: " + entry.Key + "  STATUS: " + status + " -- last read " + entry.Value.timestamp + " (" + (timeDifference.Seconds) +"seconds ago)");
                        entrycount++;
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    writeMenu = true;
                    seentags.Clear();
                    newtags.Clear();
                    ClearStdin();
                    command = "";
                    r.StartReading();
                    break;

                case "n":
                    r.StopReading();
                    Console.WriteLine("Select a tag to add:");
                    Console.WriteLine("---------------------------------");
                    string[] tagarr = newtags.ToArray();
                    for (int i = 0; i < tagarr.Length; i++)
                    {
                        string epc = tagarr[i];
                        Console.WriteLine("Tag #" + (i + 1) + ": " + epc);
                    }
                    Console.WriteLine("Press enter to cancel");
                    Console.WriteLine("---------------------------------");
                    Console.Write(" > ");
                    string response = Console.ReadLine();
                    if (response != "q" && response != "")
                    {
                        int selection;
                        bool converted = Int32.TryParse(response, out selection);
                        if (converted && selection > 0 && selection <= tagarr.Length)
                        {
                            Console.WriteLine("What do you want to call this tag?");
                            Console.Write(" > ");
                            string name = Console.ReadLine();
                            TagData dat = new TagData(name, DateTime.Now);
                            trackedtags.Add(tagarr[selection - 1], dat);
                        }
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    writeMenu = true;
                    seentags.Clear();
                    newtags.Clear();
                    ClearStdin();
                    command = "";
                    r.StartReading();
                    break;

                case "l":
                    r.StopReading();
                    int newentrycount = 1;
                    Console.WriteLine();
                    Console.WriteLine("List of tags:");
                    foreach (KeyValuePair<string, TagData> entry in trackedtags)
                    {
                        Console.WriteLine(newentrycount + ". Name: " + entry.Value.name + ":  ID: " + entry.Key);
                        newentrycount++;
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    writeMenu = true;
                    seentags.Clear();
                    newtags.Clear();
                    ClearStdin();
                    command = "";
                    r.StartReading();
                    break;

                case "":
                    break;

                default:
                    Console.Write("\nCommand not recognized. Try again.\n" +
                                  " > ");
                    command = "";
                    break;
            }

            // Check every 5 seconds if there's a tag that's missing for more than 5 seconds
            Dictionary<string, TagData> missingTags = new Dictionary<string, TagData>();
            foreach (KeyValuePair<string, TagData> entry in trackedtags)
            {
                if (DateTime.Compare(entry.Value.timestamp, DateTime.Now.AddSeconds(-5)) < 0)
                {
                    missingTags.Add(entry.Key, entry.Value);
                }
            }
            if (DateTime.Compare(DateTime.Now, next_check) > 0)
            {
                next_check = DateTime.Now.AddSeconds(5);
            
                Console.WriteLine("\n\n============================================================\n" +
                                      "                           Alerts                           \n" +
                                      "============================================================");

                if (missingTags.Count > 0) {
                    Console.WriteLine("The following tags have not been seen for at least 5 seconds:");
                    foreach (KeyValuePair<string, TagData> entry in missingTags)
                    {
                        TimeSpan timeDifference = DateTime.Now.Subtract(entry.Value.timestamp);
                        Console.WriteLine(" - Name: " + entry.Value.name + ":  ID: " + entry.Key + " -- last read " + entry.Value.timestamp + " (" + (timeDifference.Seconds) + "seconds ago)");
                    }
                } else
                {
                    Console.WriteLine(" No alerts");
                }
                Console.WriteLine("============================================================\n\n");
                writeMenu = true;
            }

            if (Console.KeyAvailable)
            {
                command = Console.ReadKey().KeyChar.ToString().ToLower();
                Console.ReadLine();
            }

            await Task.Delay(10); // Keeps the loop from maxing out the CPU
        }

        Console.WriteLine("Quit command detected, exiting....");

        // Clean up
        r.StopReading(); // Must stop continuous read before destroying
        r.Destroy();
        Console.WriteLine("Read stopped. Press Enter to exit.");
        Console.ReadLine();
    }


    private static void ClearStdin()
    {
        while (Console.In.Peek() != -1)
        {
            Console.In.Read();
        }
    }
}
