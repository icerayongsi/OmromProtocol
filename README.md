# วิธีการใช้งาน FIN Protocol

โปรแกรมนี้ใช้สำหรับติดต่อสื่อสารกับอุปกรณ์ Omron ผ่าน FIN Protocol

## การใช้งาน FINProtocolV3 Class

### การสร้าง Instance
```csharp
// สร้าง instance ใหม่
FINProtocolV3 plc = new FINProtocolV3(ipAddress, port);

// หรือใช้ Singleton pattern
FINProtocolV3 plc = FINProtocolV3.Instance;
```

### การอ่านข้อมูล
```csharp
// อ่านข้อมูลจาก DM Area
ushort address = 1000;      // เริ่มที่ DM1000
ushort itemsToRead = 10;    // อ่าน 10 words
byte[] data = plc.ReadData(address, itemsToRead);
```

### การเขียนข้อมูล
```csharp
// เขียนค่า word เดียว
ushort address = 1000;
ushort value = 123;
plc.WriteWord(address, value);

// เขียนหลาย words
ushort[] values = new ushort[] { 1, 2, 3, 4, 5 };
plc.WriteWords(address, values);

// เขียนข้อความ
string text = "Hello";
plc.WriteString(startAddress, endAddress, text);
```

### การจัดการการเชื่อมต่อ
```csharp
// ทดสอบการเชื่อมต่อ
bool isConnected = plc.TestConnection();

// ยกเลิกการเชื่อมต่อ
plc.Disconnect();

// หรือใช้ using statement
using (var plc = new FINProtocolV3(ipAddress, port))
{
    // ทำงานกับ PLC
}
```

## ตัวอย่างการใช้งานจริง

จากไฟล์ Form1.cs มีตัวอย่างการอ่านค่าต่างๆ จาก PLC:

```csharp
// อ่านค่าจาก DM Area เริ่มที่ address 1000 จำนวน 200 words
private const int PLC_START_ADDRESS = 1000;
private const int PLC_START_LIMIT_READ_ADDRESS = 200;

// อ่านข้อมูลและแปลงเป็นค่าต่างๆ
PLCData = App.PLC.ReadData(PLC_START_ADDRESS, PLC_START_LIMIT_READ_ADDRESS);

// ตัวอย่างการแปลงค่า
bool boolValue = Utilty.ReadWord(PLCData, 0, true) == 1;
float floatValue = Utilty.ToFloat(PLCData, 11, 10);
string textValue = ReadText(PLCData, 34, 43, true);
```

## ข้อควรระวัง

1. ตรวจสอบการเชื่อมต่อก่อนทำการอ่าน/เขียนข้อมูลเสมอ
2. ใช้ try-catch เพื่อจัดการข้อผิดพลาดที่อาจเกิดขึ้น
3. ปิดการเชื่อมต่อทุกครั้งหลังเลิกใช้งาน หรือใช้ using statement
4. ระวังการเขียนค่าผิด address อาจทำให้ระบบทำงานผิดพลาด
5. ตรวจสอบ timeout ในกรณีที่การสื่อสารใช้เวลานาน