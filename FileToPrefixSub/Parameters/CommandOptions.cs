using CommandLine;

namespace FileToPrefixSub.Parameters {
    public class CommandOptions {
        [Option('d', "directory", Required = false, HelpText = "The directory of the files")]
        public string Path { get; set; }
        
        [Option('r',"remove", Required = false, HelpText = "Remove prefix")]
        public bool RemovePrefix { get; set; }

        [Option('v',"verbose", Required = false, HelpText = "Verbose")]
        public bool Verbose { get; set; }
        
        [Option('l',"logfile", Required = false, HelpText = "The logfile path, if left empty it will not create a logfile")]
        public string LogFile{ get; set; }
        
        [Option('s',"split-on", Required = true, HelpText = "What to split the files on. Eg: \" - \"")]
        public string SplitOn{ get; set; }
        
        [Option("dry", Required = false, HelpText = "Dry run, no files will be changed")]
        public bool DryRun { get; set; }
    }
}
