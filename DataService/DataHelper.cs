using System;
using System.Collections.Generic;

namespace DataService
{
    public static class DataHelper
    {
     
        public static List<DriverArgumet> GetDriverArgumetByJson(bool isDesigned=false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\DriverArgumet.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\DriverArgumet.json";//绝对路径
            return Serializable.JsonStringToObject<List<DriverArgumet>>(IO.FileRead(path));
        }
        public static List<DriverMetaData> GetDriverMetaDataByJson(bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\DriverMetaData.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\DriverMetaData.json";//绝对路径
            return Serializable.JsonStringToObject<List<DriverMetaData>>(IO.FileRead(path));
        }
        public static List<TagMetaData> GetTagMetaDataByJson(bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\TagMetaData.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\TagMetaData.json";//绝对路径
            return Serializable.JsonStringToObject<List<TagMetaData>>(IO.FileRead(path));
        }
        public static List<GroupMeta> GetGroupMetaByJson(bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\GroupMeta.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\GroupMeta.json";//一个绝对路径
            return Serializable.JsonStringToObject<List<GroupMeta>>(IO.FileRead(path));
        }
        public static List<RegisterModule> GetRegisterModuleByJson(bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\RegisterModule.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\RegisterModule.json";//一个绝对路径
            return Serializable.JsonStringToObject<List<RegisterModule>>(IO.FileRead(path));
        }

        public static void SaveDriverArgumetByJson(List<DriverArgumet>  list, bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\DriverArgumet.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\DriverArgumet.json";//一个绝对路径
            IO.FileSave(path,Serializable.ObjectToJsonString(list));
        }
        public static void SaveDriverMetaDataByJson(List<DriverMetaData> list, bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\DriverMetaData.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\DriverMetaData.json";//一个绝对路径
            IO.FileSave(path, Serializable.ObjectToJsonString(list));
        }
        public static void SaveTagMetaDataByJson(List<TagMetaData> list, bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\TagMetaData.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\TagMetaData.json";//一个绝对路径
            IO.FileSave(path, Serializable.ObjectToJsonString(list));
        }
        public static void SaveGroupMetaByJson(List<GroupMeta> list, bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\GroupMeta.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\GroupMeta.json";//一个绝对路径
            IO.FileSave(path, Serializable.ObjectToJsonString(list));
        }
        public static void SaveRegisterModuleByJson(List<RegisterModule> list, bool isDesigned = false)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\JsonData\\RegisterModule.json";
            if (isDesigned) path = @"D:\00C#\SharpSCADA-master\SCADA\Program\Bin\JsonData\RegisterModule.json";//一个绝对路径
            IO.FileSave(path, Serializable.ObjectToJsonString(list));
        }

    }


    
}