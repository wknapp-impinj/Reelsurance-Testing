using CsvHelper;
using Impinj.OctaneSdk;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ReelApp
{
    internal class AppSetPassword : ReelMachine
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
        private string _newPassword = null;
        private int _antenna = 0;
        private double _txPower = 0;

        internal override Settings ConfigureSettings(Settings settings)
        {

            settings.Report.Mode = ReportMode.Individual;
            settings.RfMode = 4;
            settings.SearchMode = SearchMode.SingleTarget;
            settings.Session = 0;
            settings.TagPopulationEstimate = 1;

            if (_antenna > 0 && _txPower > 0)
            {
                settings.Antennas.DisableAll();
                settings.Antennas.AntennaConfigs.ForEach(config =>
                {
                    if (config.PortNumber == _antenna)
                    {
                        config.IsEnabled = true;
                        config.TxPowerInDbm = _txPower;
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
                AccessPassword = null,
                MemoryBank = MemoryBank.Reserved,
                WordPointer = WordPointers.AccessPassword,
                Data = TagData.FromHexString(_newPassword)
            });

            seq.Ops.Add(new TagLockOp()
            {
                AccessPasswordLockType = TagLockState.Lock,
                EpcLockType = TagLockState.Lock
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

        internal AppSetPassword(string readerAddress, int antenna, double txPower, string newPassword, string outputFile) : base(readerAddress)
        {
            this._newPassword = newPassword ?? string.Empty;
            this._antenna = antenna;
            this._txPower = txPower;
            this._resultData = new ResultData();

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
    }
}
