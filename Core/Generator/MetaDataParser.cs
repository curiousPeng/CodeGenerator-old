using Generator.Common;
using Generator.Core.Config;
using System;
using System.Collections.Generic;

namespace Generator.Core
{
    public class MetaDataParser
    {
        private IProgressBar _progress;

        public MetaDataParser(IProgressBar progress)
        {
            _progress = progress;
        }

        public void ProgressPrint(long index, long total)
        {
            if (_progress != null)
                _progress.Dispaly(Convert.ToInt32((index / (total * 1.0)) * 100));
        }
    }
}
