using System.Linq;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class OpenPyxlLoadedDrawing : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly List<OpenPyxlLoadedChart> _charts = new();
        private readonly List<OpenPyxlLoadedImage> _images = new();

        public OpenPyxlLoadedDrawing(string packagePath, string relationshipId)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public IReadOnlyList<OpenPyxlLoadedChart> Charts => _charts;

        public IReadOnlyList<OpenPyxlLoadedImage> Images => _images;

        public void AddChart(OpenPyxlLoadedChart chart)
            => _charts.Add(chart);

        public void AddImage(OpenPyxlLoadedImage image)
            => _images.Add(image);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "charts" => new PyList(_charts.Cast<object>()),
                "_charts" => new PyList(_charts.Cast<object>()),
                "images" => new PyList(_images.Cast<object>()),
                "_images" => new PyList(_images.Cast<object>()),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.drawing.spreadsheet_drawing.SpreadsheetDrawing path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlLoadedChart : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlLoadedChart(string packagePath, string relationshipId, string drawingPath)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
            DrawingPath = drawingPath;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public string DrawingPath { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "drawing_path" => PyString.FromString(ContentPath(DrawingPath)),
                "anchor" => PyNone.Instance,
                "title" => PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.chart._chart.Chart path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlLoadedImage : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlLoadedImage(string packagePath, string relationshipId, string drawingPath)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
            DrawingPath = drawingPath;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public string DrawingPath { get; }

        public string Format => PathOps.Suffix(PackagePath).TrimStart('.').ToLowerInvariant();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "drawing_path" => PyString.FromString(ContentPath(DrawingPath)),
                "format" => PyString.FromString(Format),
                "anchor" => PyNone.Instance,
                "width" => PyNone.Instance,
                "height" => PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.drawing.image.Image path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
