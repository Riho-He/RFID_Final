using System;
using System.Numerics;
using System.Threading.Tasks;
using ThingMagic;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal class Program
{
    public struct TagData
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
                trackedtags[epc].setTimestamp(DateTime.Now);
            } else {
                newtags.Enqueue(epc);
            }
        };

        bool writeMenu = true;


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
                        Console.WriteLine(entrycount + ". Name: " + entry.Value.name + ":  ID: " + entry.Key + "  STATUS: " + status + " (last read " + entry.Value.timestamp + "reads ago)");
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
                    break;
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
