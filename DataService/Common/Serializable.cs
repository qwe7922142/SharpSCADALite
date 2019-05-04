using Newtonsoft.Json;
using System;

namespace DataService
{
    /// <summary>
    /// 序列化
    /// </summary>
    public static class Serializable
    {
        #region JSON

        /// <summary>
        /// 对象序列化为JSON字符串
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ObjectToJsonString(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj, Formatting.None);
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// JSON字符串反序列化为对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        public static T JsonStringToObject<T>(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch
            {
                return default(T);
            }
        }

        #endregion

    }//end class
}
