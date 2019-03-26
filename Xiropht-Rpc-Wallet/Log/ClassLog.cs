﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Rpc_Wallet.ConsoleObject;
using Xiropht_Rpc_Wallet.Utility;

namespace Xiropht_Rpc_Wallet.Log
{
    public class ClassLogEnumeration
    {
        public const int LogIndexGeneral = 0;
        public const int LogIndexWalletUpdater = 1;
        public const int LogIndexApi = 2;

    }


    public class ClassLog
    {
        /// <summary>
        /// Log file main path.
        /// </summary>
        private const string LogDirectory = "\\Log\\";

        /// <summary>
        /// Log files paths
        /// </summary>
        private const string LogGeneral = "\\Log\\rpc-general.log"; // 0
        private const string LogWalletUpdater = "\\Log\\rpc-wallet-updater.log"; // 1
        private const string LogApi = "\\Log\\rpc-api.log"; // 2

        /// <summary>
        /// Streamwriter's 
        /// </summary>
        private static StreamWriter LogGeneralStreamWriter;
        private static StreamWriter LogWalletUpdaterStreamWriter;
        private static StreamWriter LogApiStreamWriter;

        /// <summary>
        /// Contains logs to write.
        /// </summary>
        private static List<Tuple<int, string>> ListOfLog = new List<Tuple<int, string>>(); // Structure Tuple => log id, content text.

        /// <summary>
        /// Write log settings.
        /// </summary>
        private const int WriteLogBufferSize = 8192;
        private static Thread ThreadAutoWriteLog;

        /// <summary>
        /// Log Initialization.
        /// </summary>
        /// <returns></returns>
        public static bool LogInitialization(bool fromThread = false)
        {
            try
            {
                LogInitializationFile();
                LogInitizaliationStreamWriter();
                if (!fromThread)
                {
                    AutoWriteLog();
                }
            }
            catch (Exception error)
            {
                ClassConsole.ConsoleWriteLine("Failed to initialize log system, exception error: " + error.Message, ClassConsoleEnumeration.IndexPoolConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelGeneral);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Create the log directory and log files if they not exist.
        /// </summary>
        private static bool LogInitializationFile()
        {
            if (Directory.Exists(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogDirectory)) == false)
            {
                Directory.CreateDirectory(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogDirectory));
                return false;
            }

            if (!File.Exists(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogGeneral)))
            {
                File.Create(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogGeneral)).Close();
                return false;
            }

            if (!File.Exists(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogWalletUpdater)))
            {
                File.Create(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogWalletUpdater)).Close();
                return false;
            }

            if (!File.Exists(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogApi)))
            {
                File.Create(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogApi)).Close();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Initialize stream writer's for push logs into log files.
        /// </summary>
        private static void LogInitizaliationStreamWriter()
        {
            LogApiStreamWriter?.Close();
            LogGeneralStreamWriter?.Close();
            LogWalletUpdaterStreamWriter?.Close();


            LogGeneralStreamWriter = new StreamWriter(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogGeneral), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            LogWalletUpdaterStreamWriter = new StreamWriter(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogWalletUpdater), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
            LogApiStreamWriter = new StreamWriter(ClassUtility.ConvertPath(Directory.GetCurrentDirectory() + LogApi), true, Encoding.UTF8, WriteLogBufferSize) { AutoFlush = true };
        }

        /// <summary>
        /// Insert logs inside the list of logs to write.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="logId"></param>
        public static void InsertLog(string text, int logId)
        {
            try
            {
                ListOfLog.Add(new Tuple<int, string>(logId, text));
            }
            catch
            {

            }
        }

        /// <summary>
        /// Auto write logs
        /// </summary>
        private static void AutoWriteLog()
        {
            ThreadAutoWriteLog = new Thread(async delegate ()
            {
                while (true)
                {
                    try
                    {
                        if (ListOfLog.Count > 0)
                        {
                            if (ListOfLog.Count >= 100)
                            {
                                if (!LogInitializationFile()) // Remake log files if one of them missing, close and open again streamwriter's.
                                {
                                    LogInitizaliationStreamWriter();
                                }
                                var copyOfLog = new List<Tuple<int, string>>(ListOfLog);
                                ListOfLog.Clear();
                                if (copyOfLog.Count > 0)
                                {
                                    foreach (var log in copyOfLog)
                                    {
                                        await WriteLogAsync(log.Item2, log.Item1);
                                    }
                                }
                                copyOfLog.Clear();
                            }
                        }
                    }
                    catch
                    {
                        try
                        {
                            ListOfLog.Clear();
                        }
                        catch
                        {
                            LogInitialization(true);
                        }
                    }
                    Thread.Sleep(10* 1000);
                }
            });
            ThreadAutoWriteLog.Start();
        }

        /// <summary>
        /// Write log on the selected log file in async mode.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="idLog"></param>
        private static async Task WriteLogAsync(string text, int idLog)
        {
            switch (idLog)
            {
                case ClassLogEnumeration.LogIndexGeneral:
                    await LogGeneralStreamWriter.WriteLineAsync(text);
                    break;
                case ClassLogEnumeration.LogIndexWalletUpdater:
                    await LogWalletUpdaterStreamWriter.WriteLineAsync(text);
                    break;
                case ClassLogEnumeration.LogIndexApi:
                    await LogApiStreamWriter.WriteLineAsync(text);
                    break;
            }
        }
    }
}