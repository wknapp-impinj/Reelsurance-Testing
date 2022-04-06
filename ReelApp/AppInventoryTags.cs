using CsvHelper;
using Impinj.OctaneSdk;
using OctaneSdk.Impinj.OctaneSdk;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ReelApp
{
    internal class AppInventoryTags : ReelMachine
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

        private const int SEARCH_FILTER_COUNT = 5;

        // Member Vars
        private ResultData _resultData = null;
        private CsvWriter _resultsLog = null;
        private int _antenna = 0;
        private double _txPower = 0;
        private string _tagPassword = null;
        private bool _readHidden = false;
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

            if (_readHidden)
            {
                settings.Filters.Mode = TagFilterMode.UseTagSelectFilters;

                // Add a bunch of fake filters for the reader, then add a real filter at the end
                for (int index = 1; index <= SEARCH_FILTER_COUNT; index++)
                {
                    var filter = new TagSelectFilter();

                    if (index == SEARCH_FILTER_COUNT)
                    {
                        filter.MemoryBank = MemoryBank.User;
                        filter.BitPointer = 0;
                        filter.BitCount = _tagPassword.Length * 4;
                        filter.TagMask = _tagPassword;
                        filter.MatchAction = StateUnawareAction.Select;
                        filter.NonMatchAction = StateUnawareAction.Unselect;
                    }
                    else
                    {
                        filter.MemoryBank = MemoryBank.Tid;
                        filter.BitCount = 8;
                        filter.TagMask = "FF";
                        filter.BitPointer = 0;
                        filter.MatchAction = StateUnawareAction.Select;
                        filter.NonMatchAction = StateUnawareAction.DoNothing;
                    }

                    settings.Filters.TagSelectFilters.Add(filter);
                }
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

            if (! tag.Epc.Equals(_currentEpc) && _currentState == AppState.Inventory)
            {
                _currentEpc = tag.Epc;

                TagOpSequence tagOpSequence = new TagOpSequence();
                tagOpSequence.TargetTag = new TargetTag()
                {
                    MemoryBank = MemoryBank.Tid,
                    BitPointer = 0,
                    Data = tag.Tid.ToHexString()
                };

                tagOpSequence.Ops.Add(new TagReadOp()
                {
                    MemoryBank = MemoryBank.Reserved,
                    AccessPassword = TagData.FromHexString(_tagPassword),
                    WordPointer = 2,
                    WordCount = 2,
                });

                Reader.AddOpSequence(tagOpSequence);

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
                TagReadOpResult readResult = result as TagReadOpResult;

                Console.WriteLine($"{_resultData.count}) {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff")} Elapsed:{_resultData.elapsed.TotalMilliseconds} EPC:{readResult.Tag.Epc} Password: {readResult.Data.ToHexWordString()}");
                if (_resultsLog != null)
                {
                    _resultsLog.WriteField(_resultData.count);
                    _resultsLog.WriteField(_resultData.startTime);
                    _resultsLog.WriteField(_resultData.elapsed.TotalMilliseconds);
                    _resultsLog.WriteField(readResult.Tag.Tid);
                    _resultsLog.WriteField(readResult.Tag.Epc);
                    _resultsLog.WriteField(readResult.Data.ToHexWordString());
                    _resultsLog.NextRecord();
                    _resultsLog.Flush();
                }
            });

            _currentState = AppState.Idle;
            SignalNextTag();
        }


        internal AppInventoryTags(string readerAddress, int antenna, double txPower, string tagPassword, bool readHidden, string outputFile) : base(readerAddress)
        {
            _antenna = antenna;
            _txPower = txPower;
            _tagPassword = tagPassword;
            _readHidden = readHidden;
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
                _resultsLog.WriteField("password");
                _resultsLog.NextRecord();
                _resultsLog.Flush();
            }
        }

    }
}
