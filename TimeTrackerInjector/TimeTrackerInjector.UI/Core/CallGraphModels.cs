using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace TimeTrackerInjector.UI.Core
{
  public sealed class CallGraphNode
  {
    public IMethodSymbol Symbol { get; }
    public List<CallGraphNode> Children { get; } = new();

    public CallGraphNode(IMethodSymbol symbol) => Symbol = symbol;
  }

  /// <summary>
  /// Registro dos campos Stopwatch por método (nome do campo e classe declaradora).
  /// </summary>
  public sealed class StopwatchInfo
  {
    public string DeclaringClassName { get; init; } = "";
    public string FieldName { get; init; } = ""; // ex.: sw1_ProcessItem
  }
}
