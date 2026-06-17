using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThingMagic;

internal class RFIDFinalMod
{
    private const string ReaderUri = "tmr:///com4";
    private const int ReadPowerCentiDbm = 1500; // 1500 = 15.00 dBm in the ThingMagic API
    private const int AntennaPort = 1;
    private const string DefaultShelfLocation = "Shelf 1 / Antenna 1";
    private const bool ShowEveryRawTagRead = true;

    // Passive RFID readers do not send a direct "item removed" event.
    // If a tag is not seen again within this period, the software treats it as removed.
    private static readonly TimeSpan MissingTagTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan InventoryRefreshInterval = TimeSpan.FromSeconds(2);

    private static async Task Main(string[] args)
    {
        ProductCatalog catalog = BuildProductCatalog();
        InventoryManager inventory = new InventoryManager(catalog, MissingTagTimeout);
        EnvironmentMonitor environmentMonitor = new EnvironmentMonitor(
            minTemperatureC: 2.0,
            maxTemperatureC: 30.0,
            minHumidityPercent: 30.0,
            maxHumidityPercent: 65.0);

        using (CancellationTokenSource cancellation = new CancellationTokenSource())
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };

            Reader reader = null;
            Task inventoryMonitorTask = null;

            try
            {
                reader = Reader.Create(ReaderUri);

                // Keep the transport listener on so low-level reader communication is visible.
                reader.Transport += reader.SimpleTransportListener;

                reader.Connect();
                Console.WriteLine("Connected. Configuring...");

                // 1. Set frequency region to North America.
                reader.ParamSet("/reader/region/id", Reader.Region.NA);

                // 2. Safely set power.
                reader.ParamSet("/reader/radio/readPower", ReadPowerCentiDbm);

                // 3. Explicitly tell the plan to use Antenna Port 1 and UHF EPC Gen2.
                int[] antennaList = { AntennaPort };
                SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
                reader.ParamSet("/reader/read/plan", plan);

                // 4. EVENT HANDLER: Async continuous read, with inventory logic added.
                reader.TagRead += (sender, e) =>
                {
                    string epc = e.TagReadData.EpcString;

                    if (ShowEveryRawTagRead)
                    {
                        Console.WriteLine("Found Tag EPC: " + epc);
                    }

                    inventory.RecordTagRead(epc, DefaultShelfLocation);
                };

                Console.WriteLine("Starting Continuous Async Read (like URA)...");
                reader.StartReading();

                inventoryMonitorTask = StartInventoryMonitor(inventory, cancellation.Token);
                inventory.PrintInventoryStatus(includeItemDetails: true);
                PrintCommandHelp();

                RunCommandLoop(inventory, environmentMonitor, cancellation);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RFID program error: " + ex.Message);
            }
            finally
            {
                cancellation.Cancel();

                if (inventoryMonitorTask != null)
                {
                    try
                    {
                        await inventoryMonitorTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown.
                    }
                }

                if (reader != null)
                {
                    try
                    {
                        reader.StopReading();
                    }
                    catch
                    {
                        // Reader may already be stopped or may have failed before reading began.
                    }
                }

                Console.WriteLine("Read stopped. Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }

    private static ProductCatalog BuildProductCatalog()
    {
        ProductCatalog catalog = new ProductCatalog();

        // Replace or extend these sample EPC values with the real tags used in the demo.
        // Multiple unique EPCs may point to the same product name so the count is per product.
        catalog.Add("E2000017221101441890ABCD", "Product A", minimumThreshold: 1, expectedLocation: DefaultShelfLocation);
        catalog.Add("300833B2DDD9014000000000", "Product B", minimumThreshold: 1, expectedLocation: DefaultShelfLocation);

        return catalog;
    }

    private static Task StartInventoryMonitor(InventoryManager inventory, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(InventoryRefreshInterval, token).ConfigureAwait(false);
                inventory.MarkMissingTagsAsRemoved();
            }
        }, token);
    }

    private static void RunCommandLoop(
        InventoryManager inventory,
        EnvironmentMonitor environmentMonitor,
        CancellationTokenSource cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.Write("Command> ");
            string input = Console.ReadLine();

            if (input == null)
            {
                cancellation.Cancel();
                break;
            }

            input = input.Trim();

            if (input.Length == 0)
            {
                continue;
            }

            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "status":
                    inventory.PrintInventoryStatus(includeItemDetails: true);
                    break;

                case "products":
                    inventory.PrintProductCatalog();
                    break;

                case "env":
                    HandleEnvironmentCommand(parts, environmentMonitor);
                    break;

                case "reset":
                    inventory.ClearShelfState();
                    break;

                case "help":
                    PrintCommandHelp();
                    break;

                case "quit":
                case "exit":
                    cancellation.Cancel();
                    break;

                default:
                    Console.WriteLine("Unknown command. Type 'help' to see available commands.");
                    break;
            }
        }
    }

    private static void HandleEnvironmentCommand(string[] parts, EnvironmentMonitor environmentMonitor)
    {
        if (parts.Length != 3)
        {
            Console.WriteLine("Usage: env <temperatureC> <humidityPercent>");
            return;
        }

        double temperatureC;
        double humidityPercent;

        bool parsedTemperature = double.TryParse(
            parts[1],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out temperatureC);

        bool parsedHumidity = double.TryParse(
            parts[2],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out humidityPercent);

        if (!parsedTemperature || !parsedHumidity)
        {
            Console.WriteLine("Environment values must be numbers. Example: env 22.5 45");
            return;
        }

        environmentMonitor.RecordReading(temperatureC, humidityPercent);
    }

    private static void PrintCommandHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  status              Show current inventory and low-stock state");
        Console.WriteLine("  products            Show the EPC-to-product catalog");
        Console.WriteLine("  env <tempC> <hum%>  Optional environmental monitoring check");
        Console.WriteLine("  reset               Clear local shelf state for a new demo run");
        Console.WriteLine("  help                Show this command list");
        Console.WriteLine("  quit                Stop reading and exit");
    }
}

internal sealed class InventoryManager
{
    private readonly object sync = new object();
    private readonly ProductCatalog catalog;
    private readonly TimeSpan missingTagTimeout;
    private readonly HashSet<string> currentEpcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> lastSeenByEpc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> locationByEpc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> activeLowStockAlerts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public InventoryManager(ProductCatalog catalog, TimeSpan missingTagTimeout)
    {
        this.catalog = catalog;
        this.missingTagTimeout = missingTagTimeout;
    }

    public void RecordTagRead(string epc, string location)
    {
        if (string.IsNullOrWhiteSpace(epc))
        {
            return;
        }

        string cleanEpc = epc.Trim();
        DateTime now = DateTime.Now;
        List<string> messages = new List<string>();
        bool inventoryChanged = false;

        lock (sync)
        {
            string previousLocation;
            if (currentEpcs.Contains(cleanEpc)
                && locationByEpc.TryGetValue(cleanEpc, out previousLocation)
                && !string.Equals(previousLocation, location, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add("ITEM MOVED: EPC " + cleanEpc + " from " + previousLocation + " to " + location);
                inventoryChanged = true;
            }

            lastSeenByEpc[cleanEpc] = now;
            locationByEpc[cleanEpc] = location;

            if (currentEpcs.Add(cleanEpc))
            {
                ProductInfo product = catalog.Resolve(cleanEpc);
                messages.Add("ITEM ADDED: " + product.ProductName + " | EPC " + cleanEpc + " | " + location);
                inventoryChanged = true;
            }
        }

        foreach (string message in messages)
        {
            Console.WriteLine(message);
        }

        if (inventoryChanged)
        {
            PrintInventoryStatus(includeItemDetails: false);
        }
    }

    public void MarkMissingTagsAsRemoved()
    {
        DateTime now = DateTime.Now;
        List<string> removedEpcs = new List<string>();

        lock (sync)
        {
            foreach (string epc in currentEpcs.ToList())
            {
                DateTime lastSeen;
                bool hasLastSeen = lastSeenByEpc.TryGetValue(epc, out lastSeen);

                if (!hasLastSeen || now - lastSeen > missingTagTimeout)
                {
                    currentEpcs.Remove(epc);
                    removedEpcs.Add(epc);
                }
            }
        }

        foreach (string epc in removedEpcs)
        {
            ProductInfo product = catalog.Resolve(epc);
            Console.WriteLine("ITEM REMOVED: " + product.ProductName + " | EPC " + epc);
        }

        if (removedEpcs.Count > 0)
        {
            PrintInventoryStatus(includeItemDetails: false);
        }
    }

    public void PrintInventoryStatus(bool includeItemDetails)
    {
        lock (sync)
        {
            Dictionary<string, int> counts = BuildProductCountsNoLock();

            Console.WriteLine();
            Console.WriteLine("Inventory Status @ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Console.WriteLine("Product".PadRight(20) + "Count".PadRight(10) + "Minimum".PadRight(10) + "Status");
            Console.WriteLine(new string('-', 52));

            foreach (KeyValuePair<string, int> threshold in catalog.ProductThresholds)
            {
                int count = counts.ContainsKey(threshold.Key) ? counts[threshold.Key] : 0;
                string status = count < threshold.Value ? "LOW STOCK" : "OK";
                Console.WriteLine(
                    threshold.Key.PadRight(20)
                    + count.ToString(CultureInfo.InvariantCulture).PadRight(10)
                    + threshold.Value.ToString(CultureInfo.InvariantCulture).PadRight(10)
                    + status);
            }

            int unknownCount = counts.ContainsKey(ProductCatalog.UnknownProductName)
                ? counts[ProductCatalog.UnknownProductName]
                : 0;

            if (unknownCount > 0)
            {
                Console.WriteLine(
                    ProductCatalog.UnknownProductName.PadRight(20)
                    + unknownCount.ToString(CultureInfo.InvariantCulture).PadRight(10)
                    + "N/A".PadRight(10)
                    + "ADD TO CATALOG");
            }

            if (includeItemDetails)
            {
                PrintItemDetailsNoLock();
            }

            EvaluateLowStockAlertsNoLock(counts);
        }
    }

    public void PrintProductCatalog()
    {
        Console.WriteLine();
        Console.WriteLine("Product Catalog");
        Console.WriteLine("EPC".PadRight(28) + "Product".PadRight(20) + "Minimum".PadRight(10) + "Expected Location");
        Console.WriteLine(new string('-', 86));

        foreach (ProductInfo product in catalog.AllProducts)
        {
            Console.WriteLine(
                product.Epc.PadRight(28)
                + product.ProductName.PadRight(20)
                + product.MinimumThreshold.ToString(CultureInfo.InvariantCulture).PadRight(10)
                + product.ExpectedLocation);
        }
    }

    public void ClearShelfState()
    {
        lock (sync)
        {
            currentEpcs.Clear();
            lastSeenByEpc.Clear();
            locationByEpc.Clear();
            activeLowStockAlerts.Clear();
        }

        Console.WriteLine("Local shelf state cleared.");
        PrintInventoryStatus(includeItemDetails: true);
    }

    private Dictionary<string, int> BuildProductCountsNoLock()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (string epc in currentEpcs)
        {
            ProductInfo product = catalog.Resolve(epc);

            if (!counts.ContainsKey(product.ProductName))
            {
                counts[product.ProductName] = 0;
            }

            counts[product.ProductName]++;
        }

        return counts;
    }

    private void PrintItemDetailsNoLock()
    {
        Console.WriteLine();
        Console.WriteLine("Items Currently On Shelf");
        Console.WriteLine("EPC".PadRight(28) + "Product".PadRight(20) + "Location".PadRight(22) + "Last Seen");
        Console.WriteLine(new string('-', 90));

        if (currentEpcs.Count == 0)
        {
            Console.WriteLine("No tags currently confirmed on the shelf.");
            return;
        }

        foreach (string epc in currentEpcs.OrderBy(value => value))
        {
            ProductInfo product = catalog.Resolve(epc);
            DateTime lastSeen = lastSeenByEpc.ContainsKey(epc) ? lastSeenByEpc[epc] : DateTime.MinValue;
            string location = locationByEpc.ContainsKey(epc) ? locationByEpc[epc] : "Unknown";

            Console.WriteLine(
                epc.PadRight(28)
                + product.ProductName.PadRight(20)
                + location.PadRight(22)
                + lastSeen.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    private void EvaluateLowStockAlertsNoLock(Dictionary<string, int> counts)
    {
        foreach (KeyValuePair<string, int> threshold in catalog.ProductThresholds)
        {
            int count = counts.ContainsKey(threshold.Key) ? counts[threshold.Key] : 0;
            bool isLowStock = count < threshold.Value;

            if (isLowStock && activeLowStockAlerts.Add(threshold.Key))
            {
                Console.WriteLine(
                    "LOW STOCK ALERT: "
                    + threshold.Key
                    + " has "
                    + count.ToString(CultureInfo.InvariantCulture)
                    + " item(s), below minimum "
                    + threshold.Value.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
            else if (!isLowStock && activeLowStockAlerts.Remove(threshold.Key))
            {
                Console.WriteLine("RESTOCKED: " + threshold.Key + " is back at or above the minimum threshold.");
            }
        }
    }
}

internal sealed class ProductCatalog
{
    public const string UnknownProductName = "Unknown Product";

    private readonly Dictionary<string, ProductInfo> productsByEpc =
        new Dictionary<string, ProductInfo>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> thresholdsByProduct =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<ProductInfo> AllProducts
    {
        get
        {
            return productsByEpc.Values
                .OrderBy(product => product.ProductName)
                .ThenBy(product => product.Epc)
                .ToList();
        }
    }

    public IEnumerable<KeyValuePair<string, int>> ProductThresholds
    {
        get
        {
            return thresholdsByProduct
                .OrderBy(threshold => threshold.Key)
                .ToList();
        }
    }

    public void Add(string epc, string productName, int minimumThreshold, string expectedLocation)
    {
        if (string.IsNullOrWhiteSpace(epc))
        {
            throw new ArgumentException("EPC is required.", "epc");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", "productName");
        }

        if (minimumThreshold < 0)
        {
            throw new ArgumentOutOfRangeException("minimumThreshold", "Minimum threshold cannot be negative.");
        }

        ProductInfo product = new ProductInfo(
            epc.Trim(),
            productName.Trim(),
            minimumThreshold,
            string.IsNullOrWhiteSpace(expectedLocation) ? "Unknown" : expectedLocation.Trim());

        productsByEpc[product.Epc] = product;
        thresholdsByProduct[product.ProductName] = product.MinimumThreshold;
    }

    public ProductInfo Resolve(string epc)
    {
        ProductInfo product;

        if (!string.IsNullOrWhiteSpace(epc) && productsByEpc.TryGetValue(epc.Trim(), out product))
        {
            return product;
        }

        return new ProductInfo(
            string.IsNullOrWhiteSpace(epc) ? "UNKNOWN_EPC" : epc.Trim(),
            UnknownProductName,
            minimumThreshold: 0,
            expectedLocation: "Unknown");
    }
}

internal sealed class ProductInfo
{
    public ProductInfo(string epc, string productName, int minimumThreshold, string expectedLocation)
    {
        Epc = epc;
        ProductName = productName;
        MinimumThreshold = minimumThreshold;
        ExpectedLocation = expectedLocation;
    }

    public string Epc { get; private set; }
    public string ProductName { get; private set; }
    public int MinimumThreshold { get; private set; }
    public string ExpectedLocation { get; private set; }
}

internal sealed class EnvironmentMonitor
{
    private readonly double minTemperatureC;
    private readonly double maxTemperatureC;
    private readonly double minHumidityPercent;
    private readonly double maxHumidityPercent;

    public EnvironmentMonitor(
        double minTemperatureC,
        double maxTemperatureC,
        double minHumidityPercent,
        double maxHumidityPercent)
    {
        this.minTemperatureC = minTemperatureC;
        this.maxTemperatureC = maxTemperatureC;
        this.minHumidityPercent = minHumidityPercent;
        this.maxHumidityPercent = maxHumidityPercent;
    }

    public void RecordReading(double temperatureC, double humidityPercent)
    {
        bool temperatureAbnormal = temperatureC < minTemperatureC || temperatureC > maxTemperatureC;
        bool humidityAbnormal = humidityPercent < minHumidityPercent || humidityPercent > maxHumidityPercent;

        Console.WriteLine();
        Console.WriteLine(
            "Environment Reading: "
            + temperatureC.ToString("0.0", CultureInfo.InvariantCulture)
            + " C, "
            + humidityPercent.ToString("0.0", CultureInfo.InvariantCulture)
            + "% humidity");

        if (temperatureAbnormal || humidityAbnormal)
        {
            Console.WriteLine("ENVIRONMENT ALERT: Conditions are outside the configured safe range.");
            return;
        }

        Console.WriteLine("Environment OK.");
    }
}
