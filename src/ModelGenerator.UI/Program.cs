using System.IO;
using ModelGenerator.Core.Services;
using ModelGenerator.Data.Database;
using ModelGenerator.Data.Repository;

namespace ModelGenerator.UI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        var orchestrator = new ModelOrchestrator(
            new ShapeGenerator(),
            new TextMeshConverter(),
            new SvgMeshConverter(),
            new TextPositioner(),
            new MeshComposer());

        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ModelGenerator");
        Directory.CreateDirectory(appDataDir);
        string dbPath = Path.Combine(appDataDir, "models.sqlite");

        string svgLibraryDir = Path.Combine(appDataDir, "SvgLibrary");
        Directory.CreateDirectory(svgLibraryDir);
        var svgLibraryService = new SvgLibraryService(svgLibraryDir);

        var connectionFactory = new ConnectionFactory(dbPath);
        new DatabaseInitializer(connectionFactory).Initialize();
        var repository = new SqliteModelRepository(connectionFactory);

        Application.Run(new MainForm(orchestrator, repository, svgLibraryService));
    }
}