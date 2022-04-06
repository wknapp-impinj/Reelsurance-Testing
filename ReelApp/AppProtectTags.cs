using CsvHelper;
using Impinj.OctaneSdk;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ReelApp
{
    internal class AppProtectTags : ReelMachine
    {

        private class ResultData
        {
            private Stopwatch stopwatch = new Stopwatch();
            public TimeSpan elapsed
            {
                get { return stopwatch.Elapsed; }
            }
            public DateTime startTime { get; set; }
            public int count { get; set; }
            public string epc { get; set; }
            public string tid { get; set; }
            public string message { get; set; }

            public void Reset()
            {
                stopwatch.Restart();
                startTime = DateTime.Now;
                count++;
                epc = null;
                tid = null;
                message = null;
            }

            public void StopTimer()
            {
                stopwatch.Stop();
            }

            public ResultData()
            {
                stopwatch = new Stopwatch();
                startTime = DateTime.Now;
                count = 0;
            }
        }

        // Member Vars
        private ResultData _resultData = null;
        private CsvWriter _resultsLog = null;
        private string _tagPassword = null;
        private string _newTagPassword = null;
        private int _antenna = 0;
        private double _txPower = 0;
        private bool _enable = true;

        internal override Settings ConfigureSettings(Settings settings)
        {
            if (_antenna > 0 && _txPower > 0)
            {
                settings.Antennas.AntennaConfigs.ForEach(config =>
                {
                    if (config.PortNumber == _antenna)
                    {
                        config.IsEnabled = true;
                        config.TxPowerInDbm = _txPower;
                    }
                    else
                    {
                        config.IsEnabled = false;
                    }
                });
            }
            return settings;
        }


        override internal void PerformTagOperation()
        {
            TagOpSequence seq = new TagOpSequence()
            {
                TargetTag = new TargetTag()
                {
                    MemoryBank = MemoryBank.Epc,
                    BitPointer = BitPointers.Epc,
                    Data = null
                },
            };

            seq.Ops.Add(new TagWriteOp()
            {
                AccessPassword = TagData.FromHexString(_tagPassword),
                MemoryBank = MemoryBank.Reserved,
                WordPointer = 4,
                Data = _enable ? TagData.FromHexString("0002") : TagData.FromHexString("0000")
            });

            Reader.AddOpSequence(seq);

            _resultData.Reset();

            Reader.Start();
        }

        private void Reader_TagOpComplete(ImpinjReader reader, TagOpReport report)
        {

            _resultData.StopTimer();

            reader.Stop();

            // Loop through all the completed tag operations
            report.Results.ForEach(result =>
            {
                if (result is TagWriteOpResult)
                {
                    TagWriteOpResult writeResult = result as TagWriteOpResult;

                    _resultData.epc = writeResult.Tag.Epc.ToString();
                    _resultData.tid = writeResult.Tag.Tid.ToString();
                    _resultData.message += $"WriteResultStatus={Enum.GetName(typeof(WriteResultStatus), writeResult.Result)} ";
                }
                else if (result is TagLockOpResult)
                {
                    TagLockOpResult lockResult = result as TagLockOpResult;
                    _resultData.message += $"LockResultStatus={Enum.GetName(typeof(LockResultStatus), lockResult.Result)} ";
                }
            });

            Console.WriteLine($"{_resultData.count}) {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff")} Elapsed:{_resultData.elapsed} EPC:{_resultData.epc} Message:{_resultData.message}");
            if (_resultsLog != null)
            {
                _resultsLog.WriteField(_resultData.count);
                _resultsLog.WriteField(_resultData.startTime);
                _resultsLog.WriteField(_resultData.elapsed);
                _resultsLog.WriteField(_resultData.tid);
                _resultsLog.WriteField(_resultData.epc);
                _resultsLog.WriteField(_resultData.message);
                _resultsLog.NextRecord();
                _resultsLog.Flush();
            }

            SignalNextTag();
        }

        internal AppProtectTags(string readerAddress, int antenna, double txPower, string tagPassword, string newTagPassword, string outputFile, bool enable) : base(readerAddress)
        {
            _antenna = antenna;
            _txPower = txPower;
            _tagPassword = tagPassword;
            _newTagPassword = newTagPassword;
            _resultData = new ResultData();
            _enable = enable;

            Reader.TagOpComplete += Reader_TagOpComplete;

            if (outputFile != null)
            {
                _resultsLog = new CsvWriter(new StreamWriter(outputFile), CultureInfo.InvariantCulture);
                _resultsLog.WriteField("count");
                _resultsLog.WriteField("start");
                _resultsLog.WriteField("elapsed");
                _resultsLog.WriteField("tid");
                _resultsLog.WriteField("epc");
                _resultsLog.WriteField("message");
                _resultsLog.NextRecord();
                _resultsLog.Flush();
            }
        }

        internal AppProtectTags(string readerAddress, int antenna, double txPower, string tagPassword, string newTagPassword, string outputFile) : this(readerAddress, antenna, txPower, tagPassword, newTagPassword, outputFile, true)
        {

        }
    }
}
