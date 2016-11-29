using System.Collections;
using System.Text;

namespace Microsoft.PythonTools.Profiling {
    public abstract class VTuneCommand {
        private static readonly string _vtunepath = "C:\\Program Files (x86)\\IntelSWTools\\VTune Amplifier XE 2017";
        private static readonly string _vtuneCl = _vtunepath + "\\bin32\\amplxe-cl.exe";

        protected Hashtable options = new Hashtable();
        
        public abstract string getMode();
        public virtual string get() {
            StringBuilder cmd = new StringBuilder(_vtuneCl);

            foreach (DictionaryEntry opt in options) 
            {
                cmd.Append(" ");
                cmd.Append(opt.Key);
                cmd.Append(opt.Value);
            }
            return cmd.ToString();
        }
    }
    
    public sealed class VTuneCollectCommand : VTuneCommand {
        public enum collectType { general, hotspots };
        private collectType t = collectType.hotspots;
        
        public VTuneCollectCommand(collectType _t) {
            t = _t; 
            options.Add(getMode(), "");
        } 
        
        public void setDuration(int d) {
            options.Add("-d ", d.ToString());
        }
        
        public void setUserDataDir(string d) {
            options.Add("-user-data-dir=", d);
        }
        
        public void setSearchDir(string d) {
            options.Add("-search-dir=", d);
        }

        public void setSourceSearchDir(string d) {
            options.Add("-source-search-dir=", d);
        }

        // delay collection until t seconds after target starts
        public void setResumeAfter(int t) {
            options.Add("-resume-after=", t.ToString());
        }

        private string getCollectType() {
            switch (t) {
                case collectType.general: return "general-exploration";
                default: return "hotspots";
            }
        }
        
        public override string getMode() { return "-collect " + getCollectType(); }    
        
        public override string get() {
            return base.get();
        }
    }
    
    public sealed class VTuneReportCommand : VTuneCommand {
        public enum collectType { callstacks, hotspots, hwevents, topdown };
        private collectType t = collectType.callstacks;
        
        public VTuneReportCommand(collectType _t) {
            t = _t;
            options.Add(getMode(), "");
            options.Add("-format=", "csv");
            options.Add("-csv-delimiter=", ",");
            options.Add("-report-output=", "report.csv");
        }

        public void setResultDir(string d) {
            options.Add("-r ", d);
        }
        
        public void setGroupBy(string value) {
            options.Add("-group-by ", value);
        }

        private string getCollectType() {
            switch (t) {
                case collectType.callstacks: return "callstacks";
                case collectType.hwevents: return "hw-events";
                case collectType.topdown: return "top-down";
                case collectType.hotspots: 
                default: return "hotspots";
            }
        }
        
        public override string getMode() { return "-report " + getCollectType(); }
        public override string get() {
            return base.get();
        }
    }
}