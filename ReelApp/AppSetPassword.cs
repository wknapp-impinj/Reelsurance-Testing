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

        private enum AppState
        {
            Idle,
            Inventory,
            TagOperation
        }

        // Member Vars
        private ResultData _resultData = null;
        private CsvWriter _resultsLog = null;
        private int _antenna = 0;
        private double _txPower = 0;
        private string _newPassword = null;
        private TagData _currentEpc = null;
        private AppState _currentState = AppState.Idle;

        internal override Settings ConfigureSettings(Settings settings)
        {
            settings.Report.Mode = ReportMode.Individual;
            settings.Report.IncludePcBits = false;
            settings.Report.IncludeFastId = true;

            settings.RfMode = 2;
            settings.SearchMode = SearchMode.DualTarget;
            settings.Session = 1;
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
            if (Reader.QueryStatus().IsSingulating) Reader.Stop();
            _currentState = AppState.Inventory;
            Reader.Start();
        }

        private void Reader_TagsReported(ImpinjReader reader, TagReport report)
        {
            Tag tag = report.Tags[0];

            if (!tag.Epc.Equals(_currentEpc) && _currentState == AppState.Inventory)
            {
                _currentEpc = tag.Epc;

                TagOpSequence seq = new TagOpSequence()
                {
                    TargetTag = new TargetTag()
                    {
                        MemoryBank = MemoryBank.Tid,
                        BitPointer = 0,
                        Data = tag.Tid.ToHexString()
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

                _currentState = AppState.TagOperation;
                _resultData.Reset();
            }
        }

        private void Reader_TagOpComplete(ImpinjReader reader, TagOpReport report)
        {
            _resultData.StopTimer();

            reader.Stop();

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

            _currentState = AppState.Idle;
            SignalNextTag();
        }


        internal AppSetPassword(string readerAddress, int antenna, double txPower, string newPassword, string outputFile) : base(readerAddress)
        {
            _antenna = antenna;
            _txPower = txPower;
            this._newPassword = newPassword ?? "00000000";

            _resultData = new ResultData();

            Reader.TagOpComplete += Reader_TagOpComplete;
            Reader.TagsReported += Reader_TagsReported;

            if (outputFile != null)
            {
                _resultsLog = new CsvWriter(new StreamWriter(outputFile), CultureInfo.InvariantCulture);
                _resultsLog.WriteField("count");
                _resultsLog.WriteField("start");
                _resultsLog.WriteField("elapsed");
                _resultsLog.WriteField("tid");
                _resultsLog.WriteField("epc");
                _resultsLog.NextRecord();
                _resultsLog.Flush();
            }
        }

    }
}
