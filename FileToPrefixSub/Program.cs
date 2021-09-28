using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using FileToPrefixSub.Parameters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace FileToPrefixSub {
    public class Program {
        public static void Main(string[] args) {
            var parser = Parser.Default;
            var parserResult = parser.ParseArguments<CommandOptions>(args);
            
            parserResult
                .WithParsed(opt => new Program().EntryPointAsync(opt).GetAwaiter().GetResult());
        }

        private string[] SplitFileName(string path, string splitOn) {
            var fileName = Path.GetFileName(path);
            return fileName.Split(splitOn);
        }

        private string GetPrefix(string[] fileNameParts) {
            return fileNameParts.FirstOrDefault();
        }
        
        private Task EntryPointAsync(CommandOptions options) {
            var services = ConfigureServices(options);
            var logger = services.GetRequiredService<ILogger<Program>>();
            var diOptions = services.GetRequiredService<IOptions<CommandOptions>>();
            options = diOptions.Value;

            try {
                logger.LogTrace("Settings{NewLine}{@settings}", Environment.NewLine, options);
                if (options.DryRun) {
                    logger.LogInformation("Dry run.");

                    if (!options.Verbose && string.IsNullOrWhiteSpace(options.LogFile)) {
                        logger.LogWarning("There is no logging output on this dry run.");
                        logger.LogInformation("Try to enable -v for verbose or -l for a logfile.");
                    }
                }
                var files = Directory.GetFiles(options.Path);

                var filesWithPrefixes = new Dictionary<string, List<string>>();
                foreach (var file in files) {
                    var splits = SplitFileName(file, options.SplitOn);
                    if (splits.Length <= 1) {
                        continue;
                    }

                    var prefix = GetPrefix(splits).ToLowerInvariant();

                    if (!filesWithPrefixes.TryGetValue(prefix, out var list)) {
                        logger.LogTrace("Found new prefix {prefix}", prefix);
                        list = new List<string>();
                        filesWithPrefixes.Add(prefix, list);
                    }

                    list.Add(file);
                }


                logger.LogTrace("{pCount} prefixes", filesWithPrefixes.Count);
                var toChange = filesWithPrefixes.Where(x => x.Value.Count > 1).ToList();
                logger.LogInformation("{pCount} prefixes with more then one item", toChange.Count);

                foreach (var kvp in toChange) {
                    // The key is all lowercase
                    var prefixSplits = SplitFileName(kvp.Value.First(), options.SplitOn);
                    var correctPrefix = GetPrefix(prefixSplits);

                    var newDirectory = Path.Combine(options.Path, correctPrefix);
                    if (!options.DryRun) {
                        Directory.CreateDirectory(newDirectory);
                    }

                    foreach (var oldPath in kvp.Value) {
                        var fileSplits = SplitFileName(oldPath, options.SplitOn);
                        var index = options.RemovePrefix ? 1 : 0;
                        var newFileName = string.Join(options.SplitOn, fileSplits, index, fileSplits.Length - index);
                        var newPath = Path.Combine(newDirectory, newFileName);
                        logger.LogTrace("Moving {old} to {new}", oldPath, newPath);

                        if (options.DryRun) {
                            continue;
                        }

                        try {
                            File.Move(oldPath, newPath);
                        } catch (IOException e) {
                            logger.LogWarning(e, "could not move file");
                        }
                    }
                }
                
                logger.LogInformation("Successfully ran to completion");
            } catch (Exception e) {
                logger.LogCritical(e, "Fatal error");
            }

            return Task.CompletedTask;
        }
        
         private IServiceProvider ConfigureServices(CommandOptions options) {
            Log.Logger = CreateLogger(options);

            var collection = new ServiceCollection();
            collection.AddLogging(x => {
                x.ClearProviders();
                x.AddSerilog();
            });
            
            // Set options
            collection.AddOptions<CommandOptions>().Configure(commandOptions => {
                commandOptions.Path = options.Path ?? Environment.CurrentDirectory;
                commandOptions.Verbose = options.Verbose;
                commandOptions.LogFile = options.LogFile;
                commandOptions.RemovePrefix = options.RemovePrefix;
                commandOptions.SplitOn = options.SplitOn;
                commandOptions.DryRun = options.DryRun;
            });
            
            return collection.BuildServiceProvider();
        }

        private Serilog.ILogger CreateLogger(CommandOptions options) {
            var builder = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Application", nameof(FileToPrefixSub))
                .Destructure.ToMaximumDepth(4)
                .Destructure.ToMaximumCollectionCount(10)
                .Destructure.ToMaximumStringLength(100)
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .WriteTo.Console(outputTemplate: "> {Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}", restrictedToMinimumLevel: options.Verbose ? LogEventLevel.Verbose : LogEventLevel.Information);

            if (!string.IsNullOrWhiteSpace(options.LogFile)) {
                builder.WriteTo.File(new JsonFormatter(), path: options.LogFile);
            }

            return builder.CreateLogger();
        }
    }
}
