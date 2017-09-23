using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;

using static WarshipGirlsFinalTool.helper;

namespace WarshipGirlsFinalTool
{
    public class Warshipgirls : IDisposable
    {
        /*实现IDisposable*/
        private bool disposed = false;
        /// <summary>
        /// 实现IDisposable中的Dispose方法
        /// </summary>
        public void Dispose()
        {
            //必须为true
            Dispose(true);
            //通知垃圾回收机制不再调用终结器（析构器）
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 不是必要的，提供一个Close方法仅仅是为了更符合其他语言（如C++）的规范
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// 必须，以备程序员忘记了显式调用Dispose方法
        /// </summary>
        ~Warshipgirls()
        {
            //必须为false
            Dispose(false);
        }
        /// <summary>
        /// 非密封类修饰用protected virtual
        /// 密封类修饰用private
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing)
            {
                // 清理托管资源
                //music?.Close();
                //imageFinder?.reset();
            }
            //让类型知道自己已经被释放
            disposed = true;
        }

        /*******************************/
        public enum LANG { SChinese, TChinese, English, Japanese, Thai }
        public enum ShipImageType { L, M, S }
        /*******************************/
        public string firstSever;
        public int market;
        public int channel;
        public string version;
        public string username;
        public string password;
        private string language;
        public LANG Language
        {
            set
            {
                switch (value)
                {
                    case LANG.SChinese: language = "SChinese"; break;
                    case LANG.TChinese: language = "TChinese"; break;
                    case LANG.English: language = "English"; break;
                    case LANG.Japanese: language = "Japanese"; break;
                    case LANG.Thai: language = "Thai"; break;
                    default: throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }
        private XmlDocument language_xml;

        private JsonText version_txt;
        public JsonText init_txt;
        private JsonText proj_manifest;
        public JsonText passportLogin_txt;
        private JsonText login_txt;
        public JsonText gameinfo;

        private string hf_skey;
        private string loginServer;
        private string gameServer;
        public string ResUrlWu;
        private string packageUrl;

        private readonly WebClientEx client = new WebClientEx();

        //public readonly Music music = new Music();
        //public readonly ImageFinder imageFinder = new ImageFinder();
        /*******************************/
        private string uriend()
        {
            return $"&version={version}&channel={channel}&market={market}";
        }

        private JsonText CreateJsonText(string text)
        {
            var result = new JsonText(text);
            if (result["eid"] == null) return result;
            if (init_txt != null && (string)init_txt["errorCode"][(string)result["eid"]] != null)
                throw new Exception((string)init_txt["errorCode"][(string)result["eid"]]);
            else
                throw new Exception("eid:" + result["eid"]);
        }

        public void checkVer()
        {
            client.BaseAddress = firstSever;
            version_txt = CreateJsonText(client.DownloadString($"index/checkVer/{version}/{channel}/{market}{uriend()}"));

            loginServer = (string)version_txt["loginServer"];
            if (version_txt["ResUrlWu"] != null)
            {
                ResUrlWu = (string)version_txt["ResUrlWu"];
            }
            else
            {
                ResUrlWu = (string)version_txt["ResUrl"];
            }
        }

        public void getInitConfigs()
        {
            if (File.Exists(@"documents\init.txt"))
            {
                init_txt = CreateJsonText(File.ReadAllText("documents/init.txt"));
                if ((string)init_txt["DataVersion"] == (string)version_txt["DataVersion"])
                {
                    return;
                }
            }

            client.BaseAddress = loginServer;
            init_txt = CreateJsonText(
                client.DownloadData(
                    $"index/getInitConfigs/&t={DateTime.Now.ToUTC()}&e={helper.GetNewUDID()}&gz=1{uriend()}"
                ).decompressZlibData()
            );
            File.WriteAllText("documents/init.txt", init_txt.text);
        }

        public bool checkDownload()
        {
            if (File.Exists(@"documents\proj.manifest"))
            {
                proj_manifest = CreateJsonText(File.ReadAllText("documents/proj.manifest"));
                packageUrl = (string)proj_manifest["packageUrl"];
                if ((string)proj_manifest["version"] == (string)version_txt["ResVersion"])
                {
                    return false;
                }
            }
            return true;
        }

        private class DownloadFileTask
        {
            public string uri;
            public string name;
            public long size;
            public string md5;

            public string filename => $"documents/hot/{name}";
        }

        public enum ResDownloadStage { Checking, Downloading }

        public delegate void DownloadProcessDelegate(ResDownloadStage stage, string filename, long current, long max);
        public void downloadRes(DownloadProcessDelegate callback)
        {
#if (DEBUG)
#else
            try
#endif
            {
                JsonText old_proj_manifest = proj_manifest;
                client.BaseAddress = "";
                client.DownloadString(ResUrlWu);
                proj_manifest = CreateJsonText(client.DownloadData(ResUrlWu).decompressGZipData());
                packageUrl = (string)proj_manifest["packageUrl"];
                File.WriteAllText("documents/proj.manifest", proj_manifest.text);

                //接下来比较检查每个需要下载的文件
                var filefilter = new string[]
                {

                };

                Dictionary<string, DownloadFileTask> oldlist;
                if (old_proj_manifest != null)
                {
                    oldlist = old_proj_manifest["hot"].ToDictionary(
                        file => (string)file["name"],
                        file => new DownloadFileTask
                        {
                            uri = $"{file["name"]}?md5={file["md5"]}",
                            name = (string)file["name"],
                            size = (long)file["size"],
                            md5 = (string)file["md5"]
                        }
                    );
                }
                else
                {
                    oldlist = new Dictionary<string, DownloadFileTask>();
                }

                long current = 0;
                long filenum = proj_manifest["hot"].Count();

                var toDownload = new List<DownloadFileTask>();
                foreach (var file2chk in proj_manifest["hot"])
                {
                    callback(ResDownloadStage.Checking, (string)file2chk["name"], ++current, filenum);

                    if (!(//不在过滤列表中
                            from file in filefilter
                            where file2chk["name"].ToString().ToLower().StartsWith(file.ToLower())
                            select file
                        ).Any()
                    )
                    {
                        if ( //文件不存在
                            (!File.Exists($"documents/hot/{file2chk["name"]}")) ||
                            //文件存在，但新旧文件列表中可看到文件信息并不一致
                            (
                                oldlist.ContainsKey((string)file2chk["name"]) &&
                                (string)file2chk["md5"] != oldlist[(string)file2chk["name"]].md5
                            ) ||
                            //文件存在，但旧文件列表中没有该文件，验证该文件信息发现并不相符
                            (
                                !oldlist.ContainsKey((string)file2chk["name"]) &&
                                (
                                    (long)file2chk["size"] != GetFileLength($"documents/hot/{file2chk["name"]}") ||
                                    (string)file2chk["md5"] != GetMD5FromFile($"documents/hot/{file2chk["name"]}")
                                )
                            )
                        )
                        {
                            toDownload.Add(new DownloadFileTask
                            {
                                uri = $"{file2chk["name"]}?md5={file2chk["md5"]}",
                                name = (string)file2chk["name"],
                                size = (long)file2chk["size"],
                                md5 = (string)file2chk["md5"]
                            });
                        }
                    }
                }

                long totalSize = toDownload.Sum(f => f.size);
                //开始下载
                //此处无法使用多线程并行下载：辣鸡幼明会强制断开连接
                long downloadedSize = 0;

                client.BaseAddress = packageUrl;
                foreach (var file in toDownload)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.filename));
                    client.DownloadFile(file.uri, file.filename);
                    downloadedSize += file.size;

                    callback(ResDownloadStage.Downloading, file.name, downloadedSize, totalSize);
                }

                //imageFinder.reset();
            }
#if (DEBUG)
#else
            catch (Exception e)
            {
                Debug.Fail(e.Message);
            }
#endif
        }

        public void passportLogin()
        {
            client.BaseAddress = loginServer;
            passportLogin_txt = CreateJsonText(
                client.UploadValues(
                    $"index/passportLogin/&t={DateTime.Now.ToUTC()}&e={GetNewUDID()}&gz=1{uriend()}",
                    new NameValueCollection
                    {
                        {"username", username.Base64Encode()},
                        {"pwd", password.Base64Encode()}
                    }
                ).decompressZlibData()
            );
            hf_skey = (string)passportLogin_txt["hf_skey"];
        }

        private string StdGetRequest(string command)
        {
            client.BaseAddress = gameServer;
            return client.DownloadData(
                $"{command}&t={DateTime.Now.ToUTC()}&e={GetNewUDID()}&gz=1{uriend()}"
            ).decompressZlibData();
        }

        private string StdPostRequest(string command)
        {
            client.BaseAddress = gameServer;
            return client.UploadValues(
                $"{command}&t={DateTime.Now.ToUTC()}&e={GetNewUDID()}&gz=1{uriend()}",
                new NameValueCollection
                {
                    {"pve_level", "1"}
                }
            ).decompressZlibData();
        }

        public void login(int server)
        {
            gameServer = passportLogin_txt["serverList"][server]["host"].ToString();
            login_txt = CreateJsonText(StdGetRequest(@"/index/login/" + hf_skey.Split('.')[0]));
        }

        public void initGame()
        {
            gameinfo = CreateJsonText(StdGetRequest(@"api/initGame/?&crazy=1"));
        }

        public string explore_getResult(string expID, bool messagebox = true)
        {
            JsonText Res = CreateJsonText(StdGetRequest($"explore/getResult/{expID}/"));
            if (Res["updateTaskVo"] != null)
            {
                foreach (var taskUpdate in Res["updateTaskVo"])
                {
                    var taskCondition = from task in gameinfo["taskVo"]
                                        where (string)task["taskCid"] == (string)taskUpdate["taskCid"]
                                        select task;
                    taskCondition.First()["condition"] = taskUpdate["condition"];
                }
            }
            //loveChange
            //$intimacyOther ????
            //rewardItems
            var mergeSettings = new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union
            };
            ((JObject)gameinfo["userVo"]).Merge(Res["userLevelVo"], mergeSettings);
            //((JObject)gameinfo["detailInfo"]).Merge(Res["userLevelVo"], mergeSettings);
            JToken packageVo;
            if (Res.obj.TryGetValue("packageVo", out packageVo))
            {
                foreach (var itemnew in packageVo)
                {
                    var iteminfo = from itemold in gameinfo["packageVo"]
                                   where (string)itemold["itemCid"] == (string)itemnew["itemCid"]
                                   select itemold;
                    if (iteminfo.Any())
                        iteminfo.First()["num"] = itemnew["num"];
                    else
                        ((JArray)gameinfo["packageVo"]).Add(itemnew);
                }
            }
            ((JObject)gameinfo["userVo"]).Merge(Res["userResVo"], mergeSettings);
            //newAward
            gameinfo["pveExploreVo"] = Res["pveExploreVo"];
            gameinfo["detailInfo"] = Res["detailInfo"];
            //shipVO
            gameinfo["fleetVo"] = Res["fleetVo"];
            ///////////////////////////////////////////////////////////////////
            string expTitle = (from expinfo in init_txt["pveExplore"]
                               where (string)expinfo["id"] == expID
                               select (string)expinfo["title"]).First();

            string resStr = string.Format(getLangStr("ExpeditionCompleted")
                                .Replace("%d", "{0}").Replace("%s", "{1}"),
                                Res["shipVO"]["id"], expTitle);

            if ((string)Res["bigSuccess"] == "1")
                resStr += Environment.NewLine + "大成功！";

            resStr += Environment.NewLine + "获得：" + Environment.NewLine;
            foreach (var award in (JObject)Res["newAward"])
            {
                resStr += "\t" + getCidText(int.Parse(award.Key)) + "x" + (string)award.Value + Environment.NewLine;
            }

            resStr += "舰队成员：" + Environment.NewLine;
            int shipIndex = 0;
            //int intimacyOtherIndex = 0;
            foreach (var shipID in (JArray)Res["shipVO"]["ships"])
            {
                int shipCid = (from ship in gameinfo["userShipVO"] where ship["id"].ToString() == (string)shipID select (int)ship["shipCid"]).First();
                resStr += "\t" + getCidText(shipCid) + "\t";
                if (Res["loveChange"].HasValues &&
                    Res["loveChange"][shipIndex].ToString().ToLower() != "false")
                {
                    resStr += "好感度:+" + Res["loveChange"][shipIndex] + Environment.NewLine;
                    var shipinfo = from ship in gameinfo["userShipVO"]
                                   where ship["id"].ToString() == (string)shipID
                                   select ship;
                    shipinfo.First()["love"] =
                        (int)shipinfo.First()["love"] +
                        (int)Res["loveChange"][shipIndex];
                }
                else
                {
                    resStr += Environment.NewLine;
                }
                shipIndex++;
            }
            if (messagebox)
                ;//MessageBox.Show(resStr, getLangStr("HasFinishedPVEExplore"));
            return resStr;
        }

        /*public void explore_cancel(string expID)
        {
            if (MessageBox.Show(getLangStr("PVEBackSubtitle"),
                            getLangStr("PVEBackToPort"),
                            MessageBoxButton.YesNo)
                == MessageBoxResult.Yes)
            {
                JsonText Res = CreateJsonText(StdGetRequest($"explore/cancel/{expID}/"));
                //status?
                gameinfo["pveExploreVo"] = Res["pveExploreVo"];
                gameinfo["fleetVo"] = Res["fleetVo"];
            }
        }*/

        public void explore_start(string expID, string fleetID)
        {
            JsonText Res = CreateJsonText(StdPostRequest($"explore/start/{fleetID}/{expID}/"));
            gameinfo["pveExploreVo"] = Res["pveExploreVo"];
            gameinfo["fleetVo"] = Res["fleetVo"];
        }

        public void boat_renameship(int shipid, string newname)
        {
            JsonText Res = CreateJsonText(StdGetRequest($"boat/renameShip/{shipid}/{WebUtility.UrlEncode(newname)}/"));
            //TODO:更新数据
        }

        /////////////////////////////////////////////////////////////////////

        public string getShipImgName(string shipID, ShipImageType type)
        {
            var shipinfo = (from ship in gameinfo["userShipVO"] where ship["id"].ToString() == shipID select ship).First();
            string shipModel;
            if (shipinfo["skin_cid"].ToString() != "0")
            {
                shipModel = (from skin in init_txt["ShipSkin"] where skin["cid"].ToString() == shipinfo["skin_cid"].ToString() select skin["skinId"].ToString()).First();
            }
            else
            {
                shipModel = (from ship in init_txt["shipCard"] where ship["cid"].ToString() == shipinfo["shipCid"].ToString() select ship["picId"].ToString()).First();
            }
            if (double.Parse((string)shipinfo["battleProps"]["hp"]) <= Math.Ceiling(double.Parse((string)shipinfo["battlePropsMax"]["hp"]) / 2))
            {
                shipModel = "BROKEN_" + shipModel;
            }
            else
            {
                shipModel = "NORMAL_" + shipModel;
            }
            switch (type)
            {
                case ShipImageType.L:
                    shipModel = @"documents\hot\ccbResources\model\L_" + shipModel;
                    break;
                case ShipImageType.M:
                    shipModel = @"documents\hot\ccbResources\model\M_" + shipModel;
                    break;
                case ShipImageType.S:
                    shipModel = @"documents\hot\ccbResources\model\S_" + shipModel;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return shipModel;
        }

        /*public Image getShipImage(string shipID, ShipImageType type, bool hasBG)
        {
            return imageFinder.getImage(getShipImgName(shipID, type));
        }*/

        public string getLangStr(string str)
        {
            if (language_xml == null)
            {
                language_xml = new XmlDocument();
                language_xml.Load(@"documents\hot\language.xml");
            }
            try
            {
                var node = language_xml.SelectSingleNode("root/" + str + "/" + language);
                return node?.InnerText ?? str;
            }
            catch (System.Xml.XPath.XPathException)
            {
                return str;
            }
        }

        public string getCidText(int cid)
        {
            if (cid == -1)
                return "随机物品";

            //1钻石,2燃料,3弹药,4钢材,5建材?,6经验,7船体力,8船经验,9铝材
            if (cid >= 1 && cid <= 9)
                return getLangStr("Resource" + cid);

            //141快速建造,241建造蓝图,541快速修理,66641损害管理,741装备蓝图,88841誓约之戒
            if (getLangStr("Item" + cid) != "Item" + cid)
                return getLangStr("Item" + cid);

            //船只
            var shipTitle = from ship in init_txt["shipCard"]
                            where (string)ship["cid"] == cid.ToString()
                            select (string)ship["title"];
            if (shipTitle.Any())
                return shipTitle.First();

            throw new ArgumentOutOfRangeException();

        }

        public string getShipTypeText(string type)
        {
            return getLangStr("ShipType" + type);
        }

        public string getSecretaryID()
        {
            string ret = gameinfo["secretary"].ToString();
            if (ret == "0")
            {
                ret = (from fleet in gameinfo["fleetVo"]
                       where fleet["id"].ToString() == "1"
                       select fleet["ships"][0].ToString()).First();
            }
            return ret;
        }
    }

    [DebuggerDisplay("{text}")]
    public class JsonText
    {
        public string text;
        public JObject obj;

        public JsonText(string txt)
        {
            text = txt;
            Parse();
        }

        public JToken this[string index]
        {
            get { return obj[index]; }
            set { obj[index] = value; }
        }

        public void Parse()
        {
            obj = JObject.Parse(text);
        }

        public override string ToString()
        {
            return obj.ToString();
        }
    }
}

