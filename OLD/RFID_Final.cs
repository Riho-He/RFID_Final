using System;
using System.Threading.Tasks;
using ThingMagic;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Reader r = Reader.Create("tmr:///com4");
        
        // Keep the transport listener on so you can see the magic happen
        r.Transport += r.SimpleTransportListener;

        r.Connect();
        Console.WriteLine("Connected. Configuring...");

        // 1. Set frequency region to North America
        r.ParamSet("/reader/region/id", Reader.Region.NA);

        // 2. Safely set power (1500 = 15 dBm)
        r.ParamSet("/reader/radio/readPower", 1500);

        // 3. Explicitly tell the plan to use Antenna Port 1
        int[] antennaList = { 1 };
        SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
        r.ParamSet("/reader/read/plan", plan);

        // 4. EVENT HANDLER: This is the Async "Continuous Read" logic URA uses
        r.TagRead += (sender, e) =>
        {
            Console.WriteLine($"Found Tag EPC: {e.TagReadData.EpcString}");
        };

        Console.WriteLine("Starting Continuous Async Read (like URA)...");
        
        // This fires OpCode 0x2F instead of the failing 0x22
        r.StartReading(); 

        // Keep the main thread alive for 5 seconds while the reader scans in the background
        await Task.Delay(5000);

        // Clean up
        r.StopReading();
        Console.WriteLine("Read stopped. Press Enter to exit.");
        Console.ReadLine();
    }
}
