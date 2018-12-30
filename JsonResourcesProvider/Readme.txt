JSON Resource Provider for Babylon

The provider will read all JSON files in the base directory and treat them as files containing string resources. Files are named 
using the pattern <filename>.[<culture code>].json. The provider assumes invariant strings are contained in a file with no culture
code in the file name (e.g. strings.json). All files containing culture codes (e.g. strings.de-DE.json) will be treated as translations.

Strings not present in the invariant file are ignored.

Relative paths are fully supported. Subfolders of the base directory are also processed. The name of the subfolder becomes part
of the resource name and therefore all translations of an invariant file must be placed in the same folder.

Comments are not supported.