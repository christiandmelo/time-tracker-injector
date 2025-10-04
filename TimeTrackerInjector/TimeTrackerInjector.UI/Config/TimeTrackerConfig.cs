using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeTrackerInjector.UI.Config
{
  /// <summary>
  /// Modelo de configuração do Time Tracker Injector.
  /// </summary>
  public class TimeTrackerConfig
  {
    public string SolutionFile { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string LogVariable { get; set; } = "logProcesso";
    public bool Recursive { get; set; } = true;
    public bool GlobalStopwatch { get; set; } = true;
    public bool OverwriteOriginal { get; set; } = false;
    public bool GenerateReport { get; set; } = true;
  }
}
