using Impinj.OctaneSdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReelApp
{
    class R700Reader
    {
        private ImpinjReader _reader;

        // Events
        public Action<ImpinjReader, GpiEvent> GpiChanged { get; internal set; }

        // Methods
        internal void Connect(string readerAddress)
        {
            throw new NotImplementedException();
        }

        // Constructors
        R700Reader() { }


    }
}
