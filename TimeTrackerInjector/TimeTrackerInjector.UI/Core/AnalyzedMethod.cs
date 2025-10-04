using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Representa um método identificado na análise da solution.
  /// </summary>
  public class AnalyzedMethod
  {
    public string ProjectName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public override string ToString() => $"{ClassName}.{MethodName} → {Path.GetFileName(FilePath)}";
  }
}
