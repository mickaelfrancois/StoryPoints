using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StoryPoints.Data;

/// <summary>
/// Utilisé uniquement par les outils EF Core (`dotnet ef ...`) au design-time.
/// L'application utilise <see cref="IDbContextFactory{TContext}"/> configurée dans
/// Program.cs ; les outils, eux, ont besoin de savoir construire le contexte sans
/// démarrer l'hôte web. La chaîne de connexion ici ne sert qu'à la génération de
/// migrations (qui ne touche pas la base).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=StoryPoints.db")
            .Options;

        return new AppDbContext(options);
    }
}
