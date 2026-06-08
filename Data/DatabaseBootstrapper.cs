using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace StoryPoints.Data;

public static class DatabaseBootstrapper
{
    /// <summary>
    /// Applique les migrations EF Core au démarrage, en adoptant proprement une base
    /// historique créée par l'ancien <c>EnsureCreated()</c>.
    /// <para>
    /// Une base née d'<c>EnsureCreated()</c> possède les tables mais pas la table
    /// d'historique des migrations (<c>__EFMigrationsHistory</c>). Sans précaution,
    /// <see cref="RelationalDatabaseFacadeExtensions.Migrate"/> tenterait de rejouer la
    /// migration initiale (<c>CREATE TABLE Rooms</c>) et échouerait. On « baseline »
    /// donc cette base : on crée la table d'historique et on marque la migration
    /// initiale comme déjà appliquée, pour que seules les migrations suivantes
    /// (ex. ajout de la colonne <c>Name</c>) soient appliquées — sans perte de données.
    /// </para>
    /// Cas couverts : nouvelle base (tout est créé par les migrations), base historique
    /// (adoptée puis mise à niveau), base déjà migrée (rien à faire, montée de version
    /// normale).
    /// </summary>
    public static void MigrateWithLegacyBaseline(AppDbContext db)
    {
        var history = db.GetService<IHistoryRepository>();

        // Tables présentes mais aucun historique de migrations => base héritée d'EnsureCreated.
        bool isLegacyDatabase = TableExists(db, "Rooms") && !history.Exists();
        if (isLegacyDatabase)
        {
            var initialMigrationId = db.GetService<IMigrationsAssembly>().Migrations.Keys.First();

            db.Database.ExecuteSqlRaw(history.GetCreateScript());
            db.Database.ExecuteSqlRaw(
                history.GetInsertScript(new HistoryRow(initialMigrationId, ProductInfo.GetVersion())));
        }

        db.Database.Migrate();
    }

    private static bool TableExists(AppDbContext db, string tableName) =>
        db.Database
            .SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = {0}",
                tableName)
            .Any();
}
