﻿using CommandLine;
using System;
using System.Configuration;

namespace ReelApp
{
    internal class Program
    {
        private class Options
        {
            [Option('o', "output", Required = true, HelpText = "Output CSV file name?.")]
            public string Output { get; set; }

            [Option('m', "menu", Required = true, HelpText = "Which App# do you want to run?.")]
            public int Menu { get; set; }

            [Option('r', "reader", Required = true, HelpText = "Select Reader DNS")]
            public string Reader { get; set; }

            [Option("tagpassword", Required = false, HelpText = "Tag access password")]
            public string TagPassword { get; set; }

            [Option("newtagpassword", Required = false, HelpText = "New tag access password")]
            public string NewTagPassword { get; set; }

            [Option("txpowerdbm", Required = false, HelpText = "Tx Power in dBm")]
            public double TxPowerInDbm { get; set; }

            [Option("antenna", Required = false, HelpText = "Antenna port")]
            public int Antenna { get; set; }

        }

        public static void Main(string[] args)
        {
            ReelMachine app = null;
            int menu = -1;
            string outputFile = null;
            string readerAddress = null;
            string tagPassword = null;
            string newTagPassword = null;
            double txPowerInDbm = 0;
            int antenna = 0;
            bool isError = false;

            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(option =>
                {
                    menu = option.Menu;
                    outputFile = option.Output;
                    readerAddress = option.Reader ?? ConfigurationManager.AppSettings["DefaultReaderAddress"];
                    tagPassword = option.TagPassword ?? "00000000";
                    newTagPassword = option.NewTagPassword;
                    txPowerInDbm = option.TxPowerInDbm;
                    antenna = option.Antenna;
                }).WithNotParsed(errors =>
                {
                    isError = true;
                });

            if (!isError)
            {
                switch (menu)
                {
                    case 0:
                        // Inventory tag
                        Console.WriteLine("Inventory Tags");
                        app = new AppInventoryTags(readerAddress, antenna, txPowerInDbm, outputFile);
                        break;
                    case 1:
                        // Set Tag Password
                        Console.WriteLine($"Setting Password ({newTagPassword})");
                        app = new AppSetPassword(readerAddress, antenna, txPowerInDbm, newTagPassword, outputFile);
                        break;

                    case 2:
                        Console.WriteLine("Protecting Tags");
                        app = new AppProtectTags(readerAddress, antenna, txPowerInDbm, tagPassword, newTagPassword, outputFile, true);
                        break;

                    default:
                        Console.WriteLine($"Menu option {menu} does not exist");
                        break;
                }

                app?.Start();

                Console.Write("Waiting for Reelsurance Signal. ");

            } else
            {
                Console.WriteLine("Menu Options:");
                Console.WriteLine("-------------------------------");
                Console.WriteLine("0  - Inventory Tag (reader)");
                Console.WriteLine("1  - Set Tag Password (reader, newtagpassword)");
                Console.WriteLine("2  - Protect Tag (reader, tagpassword)");
            }

            Console.WriteLine("Press any key to quit");
            Console.ReadKey();

        }
    }
}