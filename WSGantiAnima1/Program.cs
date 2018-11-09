﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WarshipGirlsFinalTool;

namespace WSGantiAnima1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("WSGantiAnima1 by Lone_Wolf");

            Directory.CreateDirectory("Documents");

            Console.Write("账号：");
            string username = Console.ReadLine();
            Console.Write("密码：");
            
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password = password.Substring(0, (password.Length - 1));
                        Console.Write("\b \b");
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();            
            var core = new Warshipgirls
            {
                username = username,
                password = password,
                version = "4.0.2",
                market = 2,
                firstSever = @"http://version.jr.moefantasy.com/"
            };
            Console.WriteLine("请选择服务器：");
            Console.WriteLine("\t1.安卓");
            Console.WriteLine("\t2.iOS");
            Console.Write("请选择：");

            redo1:
            string choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    core.channel = 100010;
                    break;
                case "2":
                    core.channel = 100111;
                    break;
                default:
                    Console.Write("无效的选项，请重新选择：");
                    goto redo1;
            }

            Console.WriteLine("获取游戏数据……");
            core.checkVer();
            core.getInitConfigs();            
            core.passportLogin();
            Console.WriteLine("请选择服务器：");
            int serverNum;
            for (serverNum = 1; serverNum <= core.passportLogin_txt["serverList"].Count(); serverNum++)
            {
                Console.WriteLine(
                    $"\t{serverNum}.{(string) core.passportLogin_txt["serverList"][serverNum -1 ]["name"]}");
            }
            Console.Write("请选择：");

            redo2:
            choice = Console.ReadLine();
            int selectedServer;
            if (int.TryParse(choice, out selectedServer) && selectedServer >= 1 && selectedServer <= serverNum)
            {
                Console.WriteLine("正在登录……");
                core.login(selectedServer - 1);
            }
            else
            {
                Console.Write("无效的选项，请重新选择：");
                goto redo2;
            }
           
            Console.WriteLine("获取玩家数据……");
            core.initGame();

            Console.WriteLine("检查船舱：");
            Console.WriteLine();
            foreach (JToken myship in core.gameinfo["userShipVO"])
            {
                string currentname = (string) myship["title"];
                int shipid = (int)myship["id"];
                Console.WriteLine($"title:{currentname}\tid:{shipid}");
                JToken shipdata = (from JToken ship in core.init_txt["shipCardWu"]
                    where ship["cid"].ToString() == myship["shipCid"].ToString()
                    select ship).FirstOrDefault();
                JToken shipdataX = (from JToken ship in core.init_txt["shipCard"]
                    where ship["cid"].ToString() == myship["shipCid"].ToString()
                    select ship).FirstOrDefault();
                int country = (int) shipdata["country"];
                Console.WriteLine($"国籍:{country}");
                if (country != 1)
                {
                    Console.WriteLine("非J国船只，跳过。");
                    Console.WriteLine("===============================");
                    continue;
                }
                string realname = shipdata["title"].ToString()
                    .Replace("日", "曰")
                    .Replace(" ", "")
                    .Replace("\t", "");
                string Xname = shipdataX["title"].ToString();
                Console.WriteLine($"真名:{realname}\t和谐名:{Xname}");
                if (currentname == realname)
                {
                    Console.WriteLine("名字正确，无需修改。");
                    Console.WriteLine("===============================");
                    continue;
                }
                if (currentname != Xname)
                {
                    Console.WriteLine("自定义船名，跳过。");
                    Console.WriteLine("===============================");
                    continue;
                }
                Console.Write("改名中……");
                core.boat_renameship(shipid, realname);
                Console.WriteLine("成功！");
                Console.WriteLine("===============================");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            Console.WriteLine("全部完成！按回车键退出！");
            Console.ReadLine();
        }
    }
}
