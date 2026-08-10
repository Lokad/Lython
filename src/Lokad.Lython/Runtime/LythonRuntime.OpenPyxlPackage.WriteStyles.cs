using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static partial class OpenPyxlPackage
    {
        private static XDocument CreateStylesXml(OpenPyxlStyleRegistry styles)
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    XlsxMain + "styleSheet",
                    styles.NumberFormats.Count == 0
                        ? null
                        : new XElement(
                            XlsxMain + "numFmts",
                            new XAttribute("count", styles.NumberFormats.Count),
                            styles.NumberFormats.OrderBy(pair => pair.Value).Select(pair => new XElement(
                                XlsxMain + "numFmt",
                                new XAttribute("numFmtId", pair.Value),
                                new XAttribute("formatCode", pair.Key)))),
                    new XElement(
                        XlsxMain + "fonts",
                        new XAttribute("count", styles.Fonts.Count + 1),
                        CreateDefaultFontXml(),
                        styles.Fonts.Select(CreateFontXml)),
                    new XElement(
                        XlsxMain + "fills",
                        new XAttribute("count", styles.Fills.Count + 2),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "none"))),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "gray125"))),
                        styles.Fills.Select(CreateFillXml)),
                    new XElement(
                        XlsxMain + "borders",
                        new XAttribute("count", styles.Borders.Count + 1),
                        CreateDefaultBorderXml(),
                        styles.Borders.Select(CreateBorderXml)),
                    new XElement(
                        XlsxMain + "cellStyleXfs",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"))),
                    new XElement(
                        XlsxMain + "cellXfs",
                        new XAttribute("count", styles.CellStyles.Count + 1),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"),
                            new XAttribute("xfId", "0")),
                        styles.CellStyles.Select(style => CreateCellFormatXml(styles, style))),
                    new XElement(
                        XlsxMain + "cellStyles",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "cellStyle",
                            new XAttribute("name", "Normal"),
                            new XAttribute("xfId", "0"),
                            new XAttribute("builtinId", "0")))));
        }

        private static XElement CreateDefaultFontXml()
            => new(
                XlsxMain + "font",
                new XElement(XlsxMain + "sz", new XAttribute("val", "11")),
                new XElement(XlsxMain + "color", new XAttribute("theme", "1")),
                new XElement(XlsxMain + "name", new XAttribute("val", "Calibri")),
                new XElement(XlsxMain + "family", new XAttribute("val", "2")),
                new XElement(XlsxMain + "scheme", new XAttribute("val", "minor")));

        private static XElement CreateFontXml(OpenPyxlStyleValue font)
        {
            var children = new List<object>();
            if (StyleBool(font, "bold"))
            {
                children.Add(new XElement(XlsxMain + "b"));
            }

            if (StyleBool(font, "italic"))
            {
                children.Add(new XElement(XlsxMain + "i"));
            }

            if (StyleBool(font, "strike"))
            {
                children.Add(new XElement(XlsxMain + "strike"));
            }

            if (StyleString(font, "underline") is { } underline)
            {
                children.Add(underline == "single"
                    ? new XElement(XlsxMain + "u")
                    : new XElement(XlsxMain + "u", new XAttribute("val", underline)));
            }

            if (StyleString(font, "sz") is { } size)
            {
                children.Add(new XElement(XlsxMain + "sz", new XAttribute("val", size)));
            }

            if (CreateColorXml("color", StyleValue(font, "color")) is { } color)
            {
                children.Add(color);
            }

            if (StyleString(font, "name") is { } name)
            {
                children.Add(new XElement(XlsxMain + "name", new XAttribute("val", name)));
            }

            return new XElement(XlsxMain + "font", children);
        }

        private static XElement CreateFillXml(OpenPyxlStyleValue fill)
        {
            var pattern = new XElement(
                XlsxMain + "patternFill",
                new XAttribute("patternType", StyleString(fill, "fill_type") ?? "none"));
            if (CreateColorXml("fgColor", StyleValue(fill, "fgColor")) is { } fgColor)
            {
                pattern.Add(fgColor);
            }

            if (CreateColorXml("bgColor", StyleValue(fill, "bgColor")) is { } bgColor)
            {
                pattern.Add(bgColor);
            }

            return new XElement(XlsxMain + "fill", pattern);
        }

        private static XElement CreateDefaultBorderXml()
            => new(
                XlsxMain + "border",
                new XElement(XlsxMain + "left"),
                new XElement(XlsxMain + "right"),
                new XElement(XlsxMain + "top"),
                new XElement(XlsxMain + "bottom"),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderXml(OpenPyxlStyleValue border)
            => new(
                XlsxMain + "border",
                CreateBorderSideXml("left", StyleValue(border, "left")),
                CreateBorderSideXml("right", StyleValue(border, "right")),
                CreateBorderSideXml("top", StyleValue(border, "top")),
                CreateBorderSideXml("bottom", StyleValue(border, "bottom")),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderSideXml(string name, object? value)
        {
            if (value is not OpenPyxlStyleValue side)
            {
                return new XElement(XlsxMain + name);
            }

            var attributes = new List<XAttribute>();
            if (StyleString(side, "style") is { } style)
            {
                attributes.Add(new XAttribute("style", style));
            }

            var element = new XElement(XlsxMain + name, attributes);
            if (CreateColorXml("color", StyleValue(side, "color")) is { } color)
            {
                element.Add(color);
            }

            return element;
        }

        private static XElement? CreateColorXml(string elementName, object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is OpenPyxlColor color)
            {
                var attributes = new List<XAttribute>();
                switch (color.Kind)
                {
                    case OpenPyxlColorKind.Indexed when color.Indexed is { } indexed:
                        attributes.Add(new XAttribute("indexed", indexed.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case OpenPyxlColorKind.Theme when color.Theme is { } theme:
                        attributes.Add(new XAttribute("theme", theme.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case OpenPyxlColorKind.Auto when color.Auto is { } auto:
                        attributes.Add(new XAttribute("auto", auto ? "1" : "0"));
                        break;
                    case OpenPyxlColorKind.Rgb:
                        if (color.Rgb is { } rgb)
                        {
                            attributes.Add(new XAttribute("rgb", rgb));
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(color), color.Kind, "Unknown openpyxl color kind.");
                }

                if (Math.Abs(color.Tint) > double.Epsilon)
                {
                    attributes.Add(new XAttribute("tint", color.Tint.ToString(CultureInfo.InvariantCulture)));
                }

                return attributes.Count == 0 ? null : new XElement(XlsxMain + elementName, attributes);
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return new XElement(XlsxMain + elementName, new XAttribute("rgb", NormalizeRgbColor(text.AsString(), "openpyxl style color", null)));
            }

            return null;
        }

        private static XElement CreateCellFormatXml(OpenPyxlStyleRegistry registry, OpenPyxlCellStyleDefinition style)
        {
            var attributes = new List<XAttribute>
            {
                new("numFmtId", registry.NumberFormatId(style.NumberFormat)),
                new("fontId", registry.FontId(style.Font)),
                new("fillId", registry.FillId(style.Fill)),
                new("borderId", registry.BorderId(style.Border)),
                new("xfId", "0"),
            };
            if (style.NumberFormat != "General")
            {
                attributes.Add(new XAttribute("applyNumberFormat", "1"));
            }

            if (style.Font is not null)
            {
                attributes.Add(new XAttribute("applyFont", "1"));
            }

            if (style.Fill is not null)
            {
                attributes.Add(new XAttribute("applyFill", "1"));
            }

            if (style.Border is not null)
            {
                attributes.Add(new XAttribute("applyBorder", "1"));
            }

            var children = new List<object>();
            if (style.Alignment is not null)
            {
                attributes.Add(new XAttribute("applyAlignment", "1"));
                children.Add(CreateAlignmentXml(style.Alignment));
            }

            if (style.Protection is not null)
            {
                attributes.Add(new XAttribute("applyProtection", "1"));
                children.Add(CreateProtectionXml(style.Protection));
            }

            return new XElement(XlsxMain + "xf", attributes, children);
        }

        private static XElement CreateAlignmentXml(OpenPyxlStyleValue alignment)
        {
            var attributes = new List<XAttribute>();
            AddOptionalAttribute(attributes, "horizontal", StyleString(alignment, "horizontal"));
            AddOptionalAttribute(attributes, "vertical", StyleString(alignment, "vertical"));
            if (StyleBool(alignment, "wrap_text"))
            {
                attributes.Add(new XAttribute("wrapText", "1"));
            }

            if (StyleBool(alignment, "shrink_to_fit"))
            {
                attributes.Add(new XAttribute("shrinkToFit", "1"));
            }

            AddOptionalAttribute(attributes, "textRotation", StyleString(alignment, "text_rotation"));
            return new XElement(XlsxMain + "alignment", attributes);
        }

        private static XElement CreateProtectionXml(OpenPyxlStyleValue protection)
            => new(
                XlsxMain + "protection",
                new XAttribute("locked", StyleBool(protection, "locked") ? "1" : "0"),
                new XAttribute("hidden", StyleBool(protection, "hidden") ? "1" : "0"));

        private static void AddOptionalAttribute(List<XAttribute> attributes, string name, string? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value));
            }
        }

        private static void AddOptionalBoolAttribute(List<XAttribute> attributes, string name, bool? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value.Value ? "1" : "0"));
            }
        }

        private static object? StyleValue(OpenPyxlStyleValue style, string name)
            => style.TryGetMember(name, out var value) && value is not PyNone ? value : null;

        private static string? StyleString(OpenPyxlStyleValue style, string name)
        {
            var value = StyleValue(style, name);
            if (value is null)
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return integer.ToString(CultureInfo.InvariantCulture);
            }

            return value is double floating
                ? floating.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        private static bool StyleBool(OpenPyxlStyleValue style, string name)
            => StyleValue(style, name) is bool value && value;

        private static string StyleValueKey(OpenPyxlStyleValue? style)
        {
            if (style is null)
            {
                return string.Empty;
            }

            return style.QualifiedName + "(" + string.Join(
                ",",
                GetOpenPyxlStyleMemberNames(style.Kind).Select(name => name + "=" + StyleObjectKey(StyleValue(style, name)))) + ")";
        }

        private static string StyleObjectKey(object? value)
        {
            if (value is null or PyNone)
            {
                return "none";
            }

            if (value is OpenPyxlStyleValue style)
            {
                return StyleValueKey(style);
            }

            if (value is OpenPyxlColor color)
            {
                return color.Key;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return "str:" + text.AsString();
            }

            if (value is bool boolean)
            {
                return boolean ? "bool:true" : "bool:false";
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return "int:" + integer.ToString(CultureInfo.InvariantCulture);
            }

            if (value is double floating)
            {
                return "float:" + floating.ToString(CultureInfo.InvariantCulture);
            }

            return value.GetType().FullName + ":" + value;
        }

    }
}
