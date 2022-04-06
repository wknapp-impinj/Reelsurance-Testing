using CsvHelper;
using Impinj.OctaneSdk;
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

        // Member Vars
        private ResultData _resultData = null;
        private CsvWriter _resultsLog = null;
        private int _antenna = 0;
        private double _txPower = 0;

        internal override Settings ConfigureSettings(Settings settings)
        {
            settings.Report.Mode = ReportMode.Individual;
            settings.RfMode = 4;
            settings.SearchMode = SearchMode.TagFocus;
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
            if (Reader.QueryStatus().IsSingulating)
            {
                Reader.Stop();
            }
            Reader.Start();
        }

        private void Reader_ReaderStarted(ImpinjReader reader, ReaderStartedEvent e)
        {
            //Reset the results (and timer) once the reader has started.
            _resultData.Reset();
        }

        private void Reader_TagsReported(ImpinjReader reader, TagReport report)
        {

            _resultData.StopTimer();

            reader.Stop();

            foreach (Tag tag in report)
            {
                Console.WriteLine($"{_resultData.count}) {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ff")} Elapsed:{_resultData.elapsed} EPC:{tag.Epc}");
                if (_resultsLog != null)
                {
                    _resultsLog.WriteField(_resultData.count);
                    _resultsLog.WriteField(_resultData.startTime);
                    _resultsLog.WriteField(_resultData.elapsed);
                    _resultsLog.WriteField(tag.Tid);
                    _resultsLog.WriteField(tag.Epc);
                    _resultsLog.NextRecord();
                    _resultsLog.Flush();
                }
            }

            SignalNextTag();
        }

        internal AppInventoryTags(string readerAddress, int antenna, double txPower, string outputFile) : base(readerAddress)
        {
            _antenna = antenna;
            _txPower = txPower;
            _resultData = new ResultData();

            Reader.TagsReported += Reader_TagsReported;
            Reader.ReaderStarted += Reader_ReaderStarted;

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
