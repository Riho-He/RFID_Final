# Smart Shelf Inventory Monitoring System

## Project Description

This project is a prototype **Smart Shelf Inventory Monitoring System** that uses **UHF RFID technology** to detect RFID-tagged products placed on a shelf. The system is designed for retail or supply-chain environments where store operators need to monitor inventory levels in real time and reduce out-of-stock situations.

The current software implementation connects to a ThingMagic-compatible RFID reader through a serial COM port and performs continuous asynchronous tag reading. When an RFID tag is detected, the program prints the tag EPC value to the console. This EPC value can be used as the unique identifier for each product or item on the smart shelf.

The current version focuses on the **RFID identification and reader communication layer** of the project. It provides the foundation for future inventory counting, threshold alerts, dashboard reporting, and optional item-location tracking.

---

## Assignment Context

The goal of the assignment is to design and build a smart shelf system that can:

- Identify and track multiple RFID-tagged products
- Maintain an inventory count for each product
- Alert the user when inventory falls below a predefined threshold
- Allow the user to check inventory status in real time
- Optionally track item movement or position
- Optionally monitor temperature and humidity conditions

This repository contains the C# RFID reader prototype code used to detect UHF RFID tags and display their EPC values.

---

## Current Features

- Connects to a ThingMagic RFID reader using the Mercury API
- Uses serial connection `tmr:///com4`
- Sets the RFID region to North America
- Sets reader power to `1500`, equal to 15 dBm
- Uses UHF EPC Gen2 protocol
- Uses antenna port 1 explicitly
- Starts continuous asynchronous RFID reading
- Prints every detected tag EPC to the console
- Stops reading after 5 seconds

---

## Hardware Requirements

- ThingMagic-compatible UHF RFID reader
- UHF RFID antenna connected to antenna port 1
- UHF RFID tags attached to products or mock products
- Windows PC or laptop
- USB/serial connection to the RFID reader

---

## Software Requirements

- C# / .NET console application
- ThingMagic Mercury API / ThingMagic library
- Visual Studio, JetBrains Rider, or .NET CLI
- Correct COM port configured for the RFID reader

---

## File Structure

```text
SmartShelfRFID/
├── RFID_Final.cs
├── README.md
└── Group Project Assignment.pdf
```

### `RFID_Final.cs`

Main C# source file. It connects to the RFID reader, configures the reader, starts continuous tag reading, prints detected EPC values, and stops after a short scan period.

### `README.md`

Project overview, setup instructions, current features, and future improvements.

---

## How the Code Works

### 1. Create the RFID Reader Object

```csharp
Reader r = Reader.Create("tmr:///com4");
```

The program creates a reader object and connects to the RFID reader through COM4.

### 2. Enable Transport Listener

```csharp
r.Transport += r.SimpleTransportListener;
```

This allows the program to show low-level communication between the computer and the reader. It is useful for debugging.

### 3. Connect to the Reader

```csharp
r.Connect();
```

This opens the connection to the RFID reader.

### 4. Set Region

```csharp
r.ParamSet("/reader/region/id", Reader.Region.NA);
```

The reader is configured for the North American UHF RFID frequency region.

### 5. Set Read Power

```csharp
r.ParamSet("/reader/radio/readPower", 1500);
```

The read power is set to 15 dBm. In the ThingMagic API, power is usually represented in centi-dBm, so `1500` means `15.00 dBm`.

### 6. Configure Read Plan

```csharp
int[] antennaList = { 1 };
SimpleReadPlan plan = new SimpleReadPlan(antennaList, TagProtocol.GEN2, null, null, 1000);
r.ParamSet("/reader/read/plan", plan);
```

The system reads from antenna port 1 using the EPC Gen2 RFID protocol.

### 7. Register Tag Read Event

```csharp
r.TagRead += (sender, e) =>
{
    Console.WriteLine($"Found Tag EPC: {e.TagReadData.EpcString}");
};
```

Whenever a tag is detected, the program prints the tag's EPC value to the console.

### 8. Start and Stop Continuous Reading

```csharp
r.StartReading();
await Task.Delay(5000);
r.StopReading();
```

The reader scans continuously for 5 seconds and then stops.

---

## How to Run

1. Connect the RFID reader to the computer.
2. Check the COM port in Windows Device Manager.
3. Update this line if your reader is not on COM4:

```csharp
Reader r = Reader.Create("tmr:///com4");
```

For example, if the reader is on COM5:

```csharp
Reader r = Reader.Create("tmr:///com5");
```

4. Make sure the ThingMagic/Mercury API library is added to the project.
5. Build and run the C# console application.
6. Place RFID-tagged products near the antenna.
7. The detected EPC values will appear in the console.

Example output:

```text
Connected. Configuring...
Starting Continuous Async Read (like URA)...
Found Tag EPC: E2000017221101441890ABCD
Found Tag EPC: 300833B2DDD9014000000000
Read stopped. Press Enter to exit.
```

---

## Current Limitations

The current code successfully demonstrates RFID tag detection, but it does not yet implement the full smart shelf inventory logic. The following features still need to be added for a complete assignment prototype:

- Product database mapping EPC values to product names
- Inventory count per product
- Added/removed item detection
- Minimum inventory threshold checking
- Low-stock alert notification
- Dashboard, email notification, or application interface
- Persistent storage of inventory data
- Optional item-location tracking
- Optional temperature and humidity monitoring

---

## Recommended Next Steps

To meet the full project requirements, the next version should extend the current RFID reading code with inventory logic.

Suggested software structure:

```text
SmartShelfRFID/
├── Program.cs
├── InventoryManager.cs
├── Product.cs
├── AlertService.cs
└── README.md
```

Suggested inventory workflow:

1. Scan RFID tags continuously.
2. Store detected EPC values in a list or dictionary.
3. Map each EPC to a product name.
4. Count how many tags belong to each product.
5. Compare each product count against the minimum threshold.
6. Display a warning if the count is below the threshold.
7. Optionally send an email or update a dashboard.

---

## Example Future Inventory Logic

```text
Detected EPC → Match EPC to Product → Update Inventory Count → Check Threshold → Display Status / Alert User
```

Example inventory table:

| EPC | Product Name | Current Count | Minimum Threshold | Status |
|---|---:|---:|---:|---|
| E2000017221101441890ABCD | Product A | 3 | 1 | In Stock |
| 300833B2DDD9014000000000 | Product B | 0 | 1 | Low Stock Alert |

---

## Testing Plan

| Test Case | Procedure | Expected Result |
|---|---|---|
| Reader connection test | Connect RFID reader and run the program | Console shows `Connected. Configuring...` |
| Single tag detection | Place one RFID tag near antenna | EPC value appears in console |
| Multiple tag detection | Place multiple RFID tags on shelf | Multiple EPC values appear in console |
| Antenna port test | Connect antenna to port 1 | Reader detects tags from antenna 1 |
| COM port test | Change COM port if needed | Reader connects only when correct COM port is used |
| Power setting test | Run with 15 dBm read power | Reader scans tags within expected range |

---

## Team Contribution Template

| Team Member | Responsibility |
|---|---|
| Member 1 | Hardware setup and RFID reader integration |
| Member 2 | C# software implementation and debugging |
| Member 3 | Inventory logic and threshold alert design |
| Member 4 | Testing, documentation, and final presentation |

---

## Future Improvements

- Add a real-time graphical dashboard
- Add product name and category mapping
- Add CSV or database-based inventory storage
- Add email or application alerts
- Add support for multiple shelves or multiple antennas
- Add item movement detection between shelves
- Add temperature and humidity monitoring
- Integrate with POS or ERP systems
- Add predictive restocking analytics
- Add anti-theft detection based on RFID movement patterns

---

## Conclusion

This prototype demonstrates the core RFID tag-reading function needed for a smart shelf inventory monitoring system. By detecting EPC values from UHF RFID tags in real time, the system establishes the foundation for automated inventory tracking. With additional inventory-counting logic, threshold alerts, and a user interface, this prototype can be expanded into a complete smart shelf solution for retail and supply-chain applications.
