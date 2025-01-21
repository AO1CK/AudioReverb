using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace AudioReverb
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 添加DLL搜索路径
            string audioSupportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NAudioSupport");
            Environment.SetEnvironmentVariable("PATH",
                Environment.GetEnvironmentVariable("PATH") + ";" + audioSupportPath);

            // 添加程序集解析事件处理
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string audioSupportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NAudioSupport");
            string assemblyName = new AssemblyName(args.Name).Name;
            string dllPath = Path.Combine(audioSupportPath, assemblyName + ".dll");

            if (File.Exists(dllPath))
            {
                return Assembly.LoadFrom(dllPath);
            }
            return null;
        }
    }
}
