using System;
using System.IO;

namespace DataService
{

    public static class IO
    {
        //同步锁
        private static readonly object syncLock = new object();

        #region 目录

        /// <summary>
        /// 获取程序当前目录，示例：
        /// c:\test\
        /// </summary>
        /// <param name="isWeb">是否为WEB程序</param>
        /// <returns></returns>
        public static string DirectoryCurrent(bool isWeb)
        {
            return System.Environment.CurrentDirectory + "\\";
        }

        /// <summary>
        /// 目录创建
        /// </summary>
        /// <param name="path">目录完整路径，如 d:\xiaoyu\dir</param>
        public static void DirectoryCreate(string path)
        {
            //提取目录
            path = Path.GetDirectoryName(path);

            //多级目录会自动逐一创建
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        /// <summary>
        /// 目录删除(包括目录下的文件及子目录)
        /// </summary>
        /// <param name="path"></param>
        public static void DirectoryDelete(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string item in Directory.GetFileSystemEntries(path))
                {
                    if (File.Exists(item))
                        File.Delete(item);
                    else
                        DirectoryDelete(item);
                }
                Directory.Delete(path, true);
            }
        }

        /// <summary>
        /// 目录及目录下的内容拷贝
        /// 示例：DirectoryXcopy("c:\a\", "d:\b\");
        /// </summary>
        /// <param name="sourceDir">源文件夹</param>
        /// <param name="targetDir">目标文件夹</param>
        public static void DirectoryCopy(string sourceDir, string targetDir)
        {
            //如果原目录存在
            if (Directory.Exists(sourceDir))
            {
                //如果目标目录不存在则创建之
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                //获取源文件夹数据
                DirectoryInfo sourceInfo = new DirectoryInfo(sourceDir);

                //文件复制
                FileInfo[] files = sourceInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    File.Copy(sourceDir + "\\" + file.Name, targetDir + "\\" + file.Name, true);
                }

                //目录复制
                DirectoryInfo[] dirs = sourceInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    string currentSource = dir.FullName;
                    string currentTarget = dir.FullName.Replace(sourceDir, targetDir);
                    Directory.CreateDirectory(currentTarget);
                    //递归
                    DirectoryCopy(currentSource, currentTarget);
                }
            }
        }

        #endregion

        #region 文件
        /// <summary>
        /// 文件保存
        /// 自动创建目录
        /// 使用UTF8编码
        /// </summary>
        /// <param name="path">完整路径</param>
        /// <param name="content">文本内容</param>
        /// <param name="isAppend">是否追加</param>
        public static string FileRead(string path)
        {
            lock (syncLock)
            {
                //创建目录
                if (File.Exists(path))
                {
                    using (StreamReader sw = new StreamReader(path, System.Text.Encoding.UTF8))
                    {
                        string str = sw.ReadToEnd();
                        sw.Close();
                        return str;
                    } 
                }
                else
                {
                    throw new Exception("文件不存在");
                }
               
            }
        }
        /// <summary>
        /// 文件保存
        /// 自动创建目录
        /// 使用UTF8编码
        /// </summary>
        /// <param name="path">完整路径</param>
        /// <param name="content">文本内容</param>
        /// <param name="isAppend">是否追加</param>
        public static void FileSave(string path, string content, bool isAppend = false)
        {
            lock (syncLock)
            {
                //创建目录
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (StreamWriter sw = new StreamWriter(path, isAppend, System.Text.Encoding.UTF8))
                {
                    sw.Write(content);
                    sw.Close();
                }
            }
        }

        /// <summary>
        /// 文件删除
        /// </summary>
        /// <param name="path">文件路径</param>
        public static void FileDelete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// 获取文件扩展名
        /// 不包括点，如：png
        /// </summary>
        /// <param name="fullName">文件全名</param>
        /// <returns></returns>
        public static string FileNameExtension(string fullName)
        {
            return fullName.Substring(fullName.LastIndexOf(".") + 1);
        }

        #endregion

    }//end class
}
