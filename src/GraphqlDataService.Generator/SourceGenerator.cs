using Scriban;
using Scriban.Runtime;

namespace GraphqlDataService.Generator;

public sealed class SourceGenerator(string templatePath)
{
    private static readonly IReadOnlyDictionary<string, string> Outputs =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Entity.scriban"] = "Data/{0}.cs",
            ["Configuration.scriban"] = "Data/{0}Configuration.cs",
            ["Inputs.scriban"] = "{0}Inputs.cs",
            ["Mapper.scriban"] = "{0}Mapper.cs",
            ["Mutation.scriban"] = "{0}Mutation.cs",
            ["Query.scriban"] = "{0}Query.cs",
            ["FilterType.scriban"] = "{0}FilterType.cs",
            ["SortType.scriban"] = "{0}SortType.cs",
            ["Subscription.scriban"] = "{0}Subscription.cs",
            ["ExportGenerator.scriban"] = "{0}ExportGenerator.cs",
            ["QueryCapabilities.scriban"] = "{0}QueryCapabilities.cs"
        };

    public async Task GenerateAsync(
        EntityModel model,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var entityDirectory = Path.Combine(outputRoot, model.EntityName);
        foreach (var output in Outputs)
        {
            var source = await File.ReadAllTextAsync(
                Path.Combine(templatePath, output.Key),
                cancellationToken);
            var template = Template.Parse(source, output.Key);
            if (template.HasErrors)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, template.Messages));
            }

            var context = new TemplateContext
            {
                MemberRenamer = member => StandardMemberRenamer.Rename(member.Name)
            };
            var script = new ScriptObject();
            script.Import(model, renamer: context.MemberRenamer);
            context.PushGlobal(script);
            var result = await template.RenderAsync(context);
            var relativePath = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                output.Value,
                model.EntityName);
            var destination = Path.Combine(entityDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, result.TrimEnd() + Environment.NewLine, cancellationToken);
        }
    }
}
