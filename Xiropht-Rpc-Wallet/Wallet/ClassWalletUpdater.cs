﻿using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;
using Xiropht_Connector_All.Wallet;
using Xiropht_Rpc_Wallet.ConsoleObject;
using Xiropht_Rpc_Wallet.Database;

namespace Xiropht_Rpc_Wallet.Wallet
{
    public class ClassWalletUpdater
    {
        private static Thread ThreadAutoUpdateWallet;
        private const string RpcTokenNetworkNotExist = "not_exist";
        private const string RpcTokenNetworkWalletAddressNotExist = "wallet_address_not_exist";

        public static void EnableAutoUpdateWallet()
        {
            if (ThreadAutoUpdateWallet != null && (ThreadAutoUpdateWallet.IsAlive || ThreadAutoUpdateWallet != null))
            {
                ThreadAutoUpdateWallet.Abort();
                GC.SuppressFinalize(ThreadAutoUpdateWallet);
            }

            ThreadAutoUpdateWallet = new Thread(async delegate ()
            {
                while (!Program.Exit)
                {
                    if (ClassRpcDatabase.RpcDatabaseContent.Count > 0)
                    {
                        string getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[0];
                        while (!await CheckTcp.CheckTcpClientAsync(getSeedNodeRandom, ClassConnectorSetting.SeedNodeTokenPort))
                        {
#if DEBUG
                            Debug.WriteLine("Seed Node host: " + getSeedNodeRandom + " is dead, check another..");
#endif
                            if (ClassConnectorSetting.SeedNodeIp.Count > 1)
                            {
                                getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[ClassUtils.GetRandomBetween(0, ClassConnectorSetting.SeedNodeIp.Count - 1)];
                            }
                            if (Program.Exit)
                            {
                                break;
                            }
                            Thread.Sleep(100);
                        }
                        try
                        {
                            foreach (var walletObject in ClassRpcDatabase.RpcDatabaseContent)
                            {
                                if (Program.Exit)
                                {
                                    break;
                                }
                                if (walletObject.Value.GetLastWalletUpdate() + 10 <= DateTimeOffset.Now.ToUnixTimeSeconds())
                                {

                                    walletObject.Value.SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds());
                                    try
                                    {
                                        await GetWalletBalanceTokenAsync(getSeedNodeRandom, walletObject.Key);
                                        if (walletObject.Value.GetWalletUniqueId() == "-1")
                                        {
                                            await GetWalletUniqueIdAsync(getSeedNodeRandom, walletObject.Key);
                                        }
                                        if (walletObject.Value.GetWalletAnonymousUniqueId() == "-1")
                                        {
                                            await GetWalletAnonymousUniqueIdAsync(getSeedNodeRandom, walletObject.Key);
                                        }
                                    }
                                    catch (Exception error)
                                    {
#if DEBUG
                                        Debug.WriteLine("Error on update wallet: " + walletObject.Key + " exception: " + error.Message);
#endif
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                    Thread.Sleep(10 * 1000);
                }
            });
            ThreadAutoUpdateWallet.Start();
        }

        public static void DisableAutoUpdateWallet()
        {
            if (ThreadAutoUpdateWallet != null && (ThreadAutoUpdateWallet.IsAlive || ThreadAutoUpdateWallet != null))
            {
                ThreadAutoUpdateWallet.Abort();
                GC.SuppressFinalize(ThreadAutoUpdateWallet);
            }
        }

        /// <summary>
        /// Get wallet token from token system.
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        private static async Task<string> GetWalletTokenAsync(string getSeedNodeRandom, string walletAddress)
        {
            string encryptedRequest = ClassRpcWalletCommand.TokenAsk + "|empty-token|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            encryptedRequest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Rijndael, encryptedRequest, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            string responseWallet = await ProceedTokenRequestAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + "|" + walletAddress + "|" + encryptedRequest);
            var responseWalletJson = JObject.Parse(responseWallet);
            responseWallet = responseWalletJson["result"].ToString();
            responseWallet = ClassAlgo.GetDecryptedResult(ClassAlgoEnumeration.Rijndael, responseWallet, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            var splitResponseWallet = responseWallet.Split(new[] { "|" }, StringSplitOptions.None);
            if ((long.Parse(splitResponseWallet[splitResponseWallet.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
            {
                if (long.Parse(splitResponseWallet[splitResponseWallet.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                {
                    return splitResponseWallet[1];
                }
                else
                {
                    return RpcTokenNetworkNotExist;
                }
            }
            else
            {
                return RpcTokenNetworkNotExist;
            }
        }

        /// <summary>
        /// Update wallet balance from token system.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        private static async Task GetWalletBalanceTokenAsync(string getSeedNodeRandom, string walletAddress)
        {
            string token = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            string encryptedRequest = ClassRpcWalletCommand.TokenAskBalance + "|" + token + "|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            encryptedRequest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Rijndael, encryptedRequest, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            string responseWallet = await ProceedTokenRequestAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + "|" + walletAddress + "|" + encryptedRequest);
            var responseWalletJson = JObject.Parse(responseWallet);
            responseWallet = responseWalletJson["result"].ToString();
            if (responseWallet != RpcTokenNetworkNotExist)
            {
                responseWallet = ClassAlgo.GetDecryptedResult(ClassAlgoEnumeration.Rijndael, responseWallet, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey() + token, ClassWalletNetworkSetting.KeySize);
                if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                {
                    string walletBalance = responseWallet;
                    var splitWalletBalance = walletBalance.Split(new[] { "|" }, StringSplitOptions.None);
                    if ((long.Parse(splitWalletBalance[splitWalletBalance.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                    {
                        if (long.Parse(splitWalletBalance[splitWalletBalance.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletBalance(splitWalletBalance[1]);
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletPendingBalance(splitWalletBalance[2]);
                        }
                        else
                        {
                            ClassConsole.ConsoleWriteLine("Wallet packet time balance token request: " + walletBalance + " is expired.", ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                        }
                    }
                    else
                    {
                        ClassConsole.ConsoleWriteLine("Wallet packet time balance token request: "+walletBalance+" is too huge", ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                    }
                }
            }
        }

        /// <summary>
        /// Update wallet unique id from token system.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        private static async Task GetWalletUniqueIdAsync(string getSeedNodeRandom, string walletAddress)
        {
            string token = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            string encryptedRequest = ClassRpcWalletCommand.TokenAskWalletId + "|" + token + "|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            encryptedRequest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Rijndael, encryptedRequest, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            string responseWallet = await ProceedTokenRequestAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + "|" + walletAddress + "|" + encryptedRequest);
            var responseWalletJson = JObject.Parse(responseWallet);
            responseWallet = responseWalletJson["result"].ToString();
            if (responseWallet != RpcTokenNetworkNotExist)
            {
                responseWallet = ClassAlgo.GetDecryptedResult(ClassAlgoEnumeration.Rijndael, responseWallet, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey() + token, ClassWalletNetworkSetting.KeySize);
                if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                {
                    string walletRequest = responseWallet;
                    var splitWalletRequest = walletRequest.Split(new[] { "|" }, StringSplitOptions.None);
                    if ((long.Parse(splitWalletRequest[splitWalletRequest.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                    {
                        if (long.Parse(splitWalletRequest[splitWalletRequest.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletUniqueId(splitWalletRequest[1]);
                        }
                        else
                        {
                            ClassConsole.ConsoleWriteLine("Wallet packet time wallet unique id token request: " + walletRequest + " is expired.", ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                        }
                    }
                    else
                    {
                        ClassConsole.ConsoleWriteLine("Wallet packet time wallet unique id token request: " + walletRequest + " is too huge", ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                    }
                }
            }
        }

        /// <summary>
        /// Update wallet anonymous unique id from token system.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        private static async Task GetWalletAnonymousUniqueIdAsync(string getSeedNodeRandom, string walletAddress)
        {
            string token = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            string encryptedRequest = ClassRpcWalletCommand.TokenAskWalletAnonymousId + "|" + token + "|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            encryptedRequest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Rijndael, encryptedRequest, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            string responseWallet = await ProceedTokenRequestAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + "|" + walletAddress + "|" + encryptedRequest);
            var responseWalletJson = JObject.Parse(responseWallet);
            responseWallet = responseWalletJson["result"].ToString();
            if (responseWallet != RpcTokenNetworkNotExist)
            {
                responseWallet = ClassAlgo.GetDecryptedResult(ClassAlgoEnumeration.Rijndael, responseWallet, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey() + token, ClassWalletNetworkSetting.KeySize);
                if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                {
                    string walletRequest = responseWallet;
                    var splitWalletRequest = walletRequest.Split(new[] { "|" }, StringSplitOptions.None);
                    if ((long.Parse(splitWalletRequest[splitWalletRequest.Length-1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                    {
                        if (long.Parse(splitWalletRequest[splitWalletRequest.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletAnonymousUniqueId(splitWalletRequest[1]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Send a transaction from a selected wallet address stored to a specific wallet address target.
        /// </summary>
        /// <param name="getSeedNodeRandom"></param>
        /// <param name="walletAddress"></param>
        /// <param name="walletAddressTarget"></param>
        /// <param name="amount"></param>
        /// <param name="fee"></param>
        /// <param name="anonymous"></param>
        /// <returns></returns>
        private static async Task<string> SendWalletTransactionTokenAsync(string getSeedNodeRandom, string walletAddress, string walletAddressTarget, string amount, string fee, bool anonymous)
        {

            string tokenWallet = await GetWalletTokenAsync(getSeedNodeRandom, walletAddress);
            string encryptedRequest = string.Empty;
            if (anonymous)
            {
                encryptedRequest = ClassRpcWalletCommand.TokenAskWalletSendTransaction + "|" + tokenWallet + "|" + walletAddressTarget + "|" + amount + "|" + fee + "|1|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            }
            else
            {
                encryptedRequest = ClassRpcWalletCommand.TokenAskWalletSendTransaction + "|" + tokenWallet + "|" + walletAddressTarget + "|" + amount + "|" + fee + "|0|" + DateTimeOffset.Now.ToUnixTimeSeconds();
            }
            encryptedRequest = ClassAlgo.GetEncryptedResult(ClassAlgoEnumeration.Rijndael, encryptedRequest, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey(), ClassWalletNetworkSetting.KeySize);
            string responseWallet = await ProceedTokenRequestAsync("http://" + getSeedNodeRandom + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/" + ClassConnectorSettingEnumeration.WalletTokenType + "|" + walletAddress + "|" + encryptedRequest);
            var responseWalletJson = JObject.Parse(responseWallet);
            responseWallet = responseWalletJson["result"].ToString();
            if (responseWallet != RpcTokenNetworkNotExist)
            {
                responseWallet = ClassAlgo.GetDecryptedResult(ClassAlgoEnumeration.Rijndael, responseWallet, walletAddress + ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletPublicKey() + tokenWallet, ClassWalletNetworkSetting.KeySize);
                if (responseWallet != ClassAlgoErrorEnumeration.AlgoError)
                {
                    string walletTransaction = responseWallet;
                    if (responseWallet != RpcTokenNetworkNotExist)
                    {
                        var splitWalletTransaction = walletTransaction.Split(new[] { "|" }, StringSplitOptions.None);
                        if ((long.Parse(splitWalletTransaction[splitWalletTransaction.Length - 1]) + 10) - DateTimeOffset.Now.ToUnixTimeSeconds() < 60)
                        {
                            if (long.Parse(splitWalletTransaction[splitWalletTransaction.Length - 1]) + 10 >= DateTimeOffset.Now.ToUnixTimeSeconds())
                            {
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletBalance(splitWalletTransaction[1]);
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletPendingBalance(splitWalletTransaction[2]);
                                ClassConsole.ConsoleWriteLine("Send transaction response " + splitWalletTransaction[0] + " from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " transaction hash: "+splitWalletTransaction[3] + "to target -> " + walletAddressTarget, ClassConsoleColorEnumeration.IndexConsoleYellowLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                                return splitWalletTransaction[0] + "|" + splitWalletTransaction[3];
                            }
                            return splitWalletTransaction[0];
                        }
                    }
                    else
                    {
                        return ClassRpcWalletCommand.SendTokenTransactionBusy;
                    }
                }
                else
                {
                    return ClassRpcWalletCommand.SendTokenTransactionBusy;
                }
            }

            ClassConsole.ConsoleWriteLine("Send transaction refused from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget, ClassConsoleColorEnumeration.IndexConsoleYellowLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
            return ClassRpcWalletCommand.SendTokenTransactionRefused;
        }

        /// <summary>
        /// Proceed token request throught http protocol.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static async Task<string> ProceedTokenRequestAsync(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ServicePoint.Expect100Continue = false;
            request.KeepAlive = false;
            request.Timeout = 2000;
            request.UserAgent = ClassConnectorSetting.CoinName + " RPC Wallet - " + Assembly.GetExecutingAssembly().GetName().Version + "R";

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Send a transaction with token system with a selected wallet address, amount and fee.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public static async Task<string> ProceedTransactionTokenRequestAsync(string walletAddress, string amount, string fee, string walletAddressTarget, bool anonymous)
        {
            if (anonymous)
            {
                ClassConsole.ConsoleWriteLine("Attempt to send transaction from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget, ClassConsoleColorEnumeration.IndexConsoleYellowLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
            }
            else
            {
                ClassConsole.ConsoleWriteLine("Attempt to send transaction from wallet address " + walletAddress + " of amount " + amount + " " + ClassConnectorSetting.CoinNameMin + " fee " + fee + " " + ClassConnectorSetting.CoinNameMin + " to target -> " + walletAddressTarget, ClassConsoleColorEnumeration.IndexConsoleYellowLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
            }
            if (ClassRpcDatabase.RpcDatabaseContent.ContainsKey(walletAddress))
            {
                if (!ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletOnSendTransactionStatus())
                {
                    ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(true);
                    decimal balanceFromDatabase = decimal.Parse(ClassRpcDatabase.RpcDatabaseContent[walletAddress].GetWalletBalance().Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    decimal balanceFromRequest = decimal.Parse(amount.Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    decimal feeFromRequest = decimal.Parse(fee.Replace(".", ","), NumberStyles.Currency, Program.GlobalCultureInfo);
                    if (!anonymous)
                    {
                        if (balanceFromRequest + feeFromRequest > balanceFromRequest + ClassConnectorSetting.MinimumWalletTransactionFee)
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                            return ClassRpcWalletCommand.SendTokenTransactionRefused;
                        }
                        else
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds());
                            string getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[0];
                            if (ClassConnectorSetting.SeedNodeIp.Count > 1)
                            {
                                getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[ClassUtils.GetRandomBetween(0, ClassConnectorSetting.SeedNodeIp.Count - 1)];
                            }
                            try
                            {
                                return await SendWalletTransactionTokenAsync(getSeedNodeRandom, walletAddress, walletAddressTarget, amount, fee, anonymous);
                            }
                            catch (Exception error)
                            {
                                ClassConsole.ConsoleWriteLine("Error on send transaction from wallet: " + ClassRpcDatabase.RpcDatabaseContent[walletAddress] + " exception: " + error.Message, ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                                return ClassRpcWalletCommand.SendTokenTransactionRefused;
                            }
                        }
                    }
                    else
                    {
                        if (balanceFromRequest + feeFromRequest + ClassConnectorSetting.MinimumWalletTransactionAnonymousFee > balanceFromRequest + ClassConnectorSetting.MinimumWalletTransactionFee + ClassConnectorSetting.MinimumWalletTransactionAnonymousFee)
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                            return ClassRpcWalletCommand.SendTokenTransactionRefused;
                        }
                        else
                        {
                            ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetLastWalletUpdate(DateTimeOffset.Now.ToUnixTimeSeconds());
                            string getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[0];
                            if (ClassConnectorSetting.SeedNodeIp.Count > 1)
                            {
                                getSeedNodeRandom = ClassConnectorSetting.SeedNodeIp[ClassUtils.GetRandomBetween(0, ClassConnectorSetting.SeedNodeIp.Count - 1)];
                            }
                            try
                            {
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                                return await SendWalletTransactionTokenAsync(getSeedNodeRandom, walletAddress, walletAddressTarget, amount, fee, anonymous);
                            }
                            catch (Exception error)
                            {
                                ClassConsole.ConsoleWriteLine("Error on send transaction from wallet: " + ClassRpcDatabase.RpcDatabaseContent[walletAddress] + " exception: " + error.Message, ClassConsoleColorEnumeration.IndexConsoleRedLog, ClassConsoleLogLevelEnumeration.LogLevelWalletObject);
                                ClassRpcDatabase.RpcDatabaseContent[walletAddress].SetWalletOnSendTransactionStatus(false);
                                return ClassRpcWalletCommand.SendTokenTransactionRefused;
                            }
                        }
                    }
                }
                else
                {
                    return ClassRpcWalletCommand.SendTokenTransactionBusy;
                }
            }

            return RpcTokenNetworkWalletAddressNotExist;

        }
    }
}
