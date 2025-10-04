using System;
using System.IO;
using Newtonsoft.Json;

namespace TimeTrackerInjector.UI.Config
{
  /// <summary>
  /// Gerencia a leitura e escrita do arquivo de configuração do Time Tracker Injector.
  /// </summary>
  public class ConfigurationManager
  {
    private readonly string _configPath;

    /// <summary>
    /// Representa a configuração atual carregada na memória.
    /// </summary>
    public TimeTrackerConfig Current { get; private set; }

    public ConfigurationManager(string? configPath = null)
    {
      _configPath = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timetrackerconfig.json");
      Load();
    }

    /// <summary>
    /// Lê o arquivo de configuração. Caso não exista, cria um arquivo padrão.
    /// </summary>
    public void Load()
    {
      if (!File.Exists(_configPath))
      {
        Current = CreateDefaultConfig();
        Save();
        return;
      }

      try
      {
        var json = File.ReadAllText(_configPath);
        Current = JsonConvert.DeserializeObject<TimeTrackerConfig>(json) ?? CreateDefaultConfig();
      }
      catch
      {
        Current = CreateDefaultConfig();
        Save();
      }
    }

    /// <summary>
    /// Persiste a configuração atual no arquivo JSON.
    /// </summary>
    public void Save()
    {
      var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
      File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// Retorna uma nova configuração padrão.
    /// </summary>
    private static TimeTrackerConfig CreateDefaultConfig() => new()
    {
      SolutionFile = string.Empty,
      ProjectName = string.Empty,
      ClassName = string.Empty,
      MethodName = string.Empty,
      LogVariable = "logProcesso",
      Recursive = true,
      GlobalStopwatch = true,
      OverwriteOriginal = false,
      GenerateReport = true
    };
  }
}


