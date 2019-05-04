using System;
using System.Runtime.InteropServices;

namespace DataService
{
    public class DriverArgumet
    {
        public short DriverID;
        public string PropertyName;
        public string PropertyValue;

        public DriverArgumet(short id, string name, string value)
        {
            DriverID = id;
            PropertyName = name;
            PropertyValue = value;
        }
    }
    public class TagMetaData : IComparable<TagMetaData>
    {
        public short ID { set; get; }
        public string Name { set; get; }
        public byte DataTypeNum
        {
            set { DataType = (DataType)value; }
            get { return (byte)DataType; }
        }
        public DataType DataType { set; get; }
        public ushort Size { set; get; }
        public string Address { set; get; }
        public short GroupID { set; get; }
        public bool Active { set; get; }
        public string Description { set; get; }
        public float Maximum { set; get; }
        public float Minimum { set; get; }
        public int Cycle { set; get; }

        public TagMetaData()
        {
        }
        public TagMetaData(short id, short grpId, string name, string address,
            DataType type, ushort size, bool archive = false, float max = 0,
            float min = 0, int cycle = 0)
        {
            ID = id;
            GroupID = grpId;
            Name = name;
            Address = address;
            DataType = type;
            Size = size;
            Active = archive;
            Maximum = max;
            Minimum = min;
            Cycle = cycle;
        }
        public int CompareTo(TagMetaData other)
        {
            return this.ID.CompareTo(other.ID);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Scaling : IComparable<Scaling>
    {
        public short ID;

        public ScaleType ScaleType;

        public float EUHi;

        public float EULo;

        public float RawHi;

        public float RawLo;

        public Scaling(short id, ScaleType type, float euHi, float euLo, float rawHi, float rawLo)
        {
            ID = id;
            ScaleType = type;
            EUHi = euHi;
            EULo = euLo;
            RawHi = rawHi;
            RawLo = rawLo;
        }

        public int CompareTo(Scaling other)
        {
            return ID.CompareTo(other.ID);
        }

        public static readonly Scaling Empty = new Scaling { ScaleType = ScaleType.None };
    }

    public struct ItemData<T>
    {
        public T Value;
        public long TimeStamp;
        public QUALITIES Quality;

        public ItemData(T value, long timeStamp, QUALITIES quality)
        {
            Value = value;
            TimeStamp = timeStamp;
            Quality = quality;
        }
    }

    public enum ScaleType : byte
    {
        None = 0,
        Linear = 1,
        SquareRoot = 2
    }
    public class GroupMeta
    {
        public string Name { set; get; }
        public short ID { set; get; }
        public short DriverID { set; get; }

        public int UpdateRate { set; get; }
        public float DeadBand { set; get; }
        public bool Active { set; get; }
    }
    public class DriverMetaData
    {
        public short ID { set; get; }
        public int DriverID { set; get; }
        public string Name { set; get; }
        public string Assembly { set; get; }
        public string ClassName { set; get; }
        public object Target { get; set; }
    }
    public class RegisterModule
    {
        public int DriverID { get; set; }

        public string AssemblyName { get; set; }

        public string Description { get; set; }

        public string ClassName { get; set; }

        public string ClassFullName { get; set; }

    }
}
