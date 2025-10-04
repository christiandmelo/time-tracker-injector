using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.MSBuild;
using global::TimeTrackerInjector.UI.Config;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Responsável por carregar a solution e localizar o método inicial configurado.
  /// </summary>
  public class SolutionAnalyzer
  {
    private readonly TimeTrackerConfig _config;
    private readonly List<AnalyzedMethod> _methodsFound = new();

    public SolutionAnalyzer(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));

      if (!MSBuildLocator.IsRegistered)
        MSBuildLocator.RegisterDefaults();
    }

    /// <summary>
    /// Executa a análise da solution e retorna os métodos encontrados.
    /// </summary>
    public async Task<IReadOnlyList<AnalyzedMethod>> AnalyzeAsync()
    {
      _methodsFound.Clear();

      if (string.IsNullOrWhiteSpace(_config.SolutionFile))
        throw new InvalidOperationException("Caminho da solution não configurado.");

      if (!File.Exists(_config.SolutionFile))
        throw new FileNotFoundException($"Solution não encontrada: {_config.SolutionFile}");

      using var workspace = MSBuildWorkspace.Create();
      var solution = await workspace.OpenSolutionAsync(_config.SolutionFile);

      var targetProject = solution.Projects.FirstOrDefault(p =>
          p.Name.Equals(_config.ProjectName, StringComparison.OrdinalIgnoreCase));

      if (targetProject == null)
        throw new InvalidOperationException($"Projeto '{_config.ProjectName}' não encontrado na solution.");

      var compilation = await targetProject.GetCompilationAsync();

      if (compilation == null)
        throw new InvalidOperationException("Falha ao compilar projeto para análise.");

      // Localiza a classe configurada
      var classSymbol = compilation.GetSymbolsWithName(_config.ClassName)
                                   .OfType<INamedTypeSymbol>()
                                   .FirstOrDefault();

      if (classSymbol == null)
        throw new InvalidOperationException($"Classe '{_config.ClassName}' não encontrada no projeto.");

      // Localiza o método configurado
      var methodSymbol = classSymbol.GetMembers()
                                    .OfType<IMethodSymbol>()
                                    .FirstOrDefault(m => m.Name.Equals(_config.MethodName, StringComparison.OrdinalIgnoreCase));

      if (methodSymbol == null)
        throw new InvalidOperationException($"Método '{_config.MethodName}' não encontrado na classe '{_config.ClassName}'.");

      // Adiciona o método base
      _methodsFound.Add(new AnalyzedMethod
      {
        ClassName = classSymbol.Name,
        MethodName = methodSymbol.Name,
        FilePath = methodSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
        ProjectName = _config.ProjectName
      });

      // Se habilitado, realiza a análise recursiva de chamadas
      if (_config.Recursive)
      {
        var callGraph = new CallGraphBuilder(compilation);
        var relatedMethods = await callGraph.BuildAsync(methodSymbol);

        // Adiciona apenas métodos únicos que não estão na lista
        foreach (var m in relatedMethods)
        {
          if (!_methodsFound.Any(x =>
              x.ClassName == m.ClassName &&
              x.MethodName == m.MethodName &&
              x.FilePath == m.FilePath))
          {
            _methodsFound.Add(m);
          }
        }
      }


      return _methodsFound;
    }
  }
}
