using System;
using System.Collections.Generic;
using System.Linq;
using Babylon.ResourcesProvider;
using System.IO;
using Newtonsoft.Json;

namespace JsonResourceProvider
{
    /// <summary>
    /// JSON Resource Provider for Babylon.NET
    /// 
    /// The provider will read all JSON files in the base directory and treat them as files containing string resources.Files are named
    /// using the pattern<filename>.[culture code].json.The provider assumes invariant strings are contained in a file with no culture
    /// code in the file name(e.g.strings.json). All files containing culture codes(e.g.strings.de-DE.json) will be treated as translations.
    ///  
    /// Strings not present in the invariant file are ignored.
    /// 
    /// Relative paths are fully supported.Subfolders of the base directory are also processed.The name of the subfolder becomes part
    /// of the resource name and therefore all translations of an invariant file must be placed in the same folder.
    /// 
    /// Comments are not supported.    
    /// </summary>
    public class JsonResourcesProvider : IResourcesProvider
    {
        string _storageLocation;

        /// <summary>
        /// The StorageLocation will be set by the user when creating a new generic localization project in Babylon.NET. It can be a path to a folder, a file name,
        /// a database connection string or any other information needed to access the resource files.
        /// </summary>
        public string StorageLocation
        {
            get
            {
                return _storageLocation;
            }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentNullException(value);

                _storageLocation = value;
            }
        }

        /// <summary>
        /// This text is displayed to the user as label to the storage location textbox/combobox when setting up the resource provider.
        /// </summary>
        public string StorageLocationUserText
        {
            get
            {
                return "Base Directory where language files are located";
            }
        }

        /// <summary>
        /// This is the type of storage used be the provider. Depending on the type Babylon.NET will display a FileSelectionControl, a DirectorySelectionControl 
        /// or a simple TextBox as StorageLocation input control.
        /// </summary>
        public StorageType StorageType
        {
            get
            {
                return StorageType.Directory;
            }
        }

        /// <summary>
        /// This is the description of the Resource Provider Babylon.NET will display when selecting a Resource Provider
        /// </summary>
        public string Description
        {
            get
            {
                return "Standard JSON Resources Provider. Every JSON file contains one language.";
            }
        }

        /// <summary>
        /// This is the name of the Resource Provider Babylon.NET will display when selecting a Resource Provider
        /// </summary>
        public string Name
        {
            get
            {
                return "JSON Resources Provider";
            }
        }

        /// <summary>
        /// Babylon.NET will pass the path to the current solution to the provider. This can for example be used to work with relative paths.
        /// </summary>
        public string SolutionPath { get; set; }

        /// <summary>
        /// Babylon.NET will call this method when the resource files should be written.
        /// </summary>
        /// <param name="projectName">Name of the project whose resources are exported.</param>
        /// <param name="resourceStrings">A list of resource strings with related translations.</param>
        /// <param name="resultDelegate">Delegate to return the status of the export.</param>
        public void ExportResourceStrings(string projectName, ICollection<StringResource> resourceStrings, ResourceStorageOperationResultDelegate resultDelegate)
        {
            // We use a dictionary as cache for the resources for each file
            Dictionary<string, Dictionary<string, string>> fileCache = new Dictionary<string, Dictionary<string, string>>();

            // We keep an error list with files that cannot be written to avoid the same error over and over
            List<string> errorList = new List<string>();

            // convert relative storage location into absolute one
            string baseDirectory = GetBaseDirectory();

            // loop over all strings...
            foreach (var resString in resourceStrings)
            {
                // ... and all locales. Babylon.NET uses an empty string as locale for the invariant language.
                foreach (string locale in resString.GetLocales())
                {
                    // assemble file name
                    string filename = Path.Combine(baseDirectory, string.Format("{0}.{1}.json", resString.StorageLocation, locale)).Replace("..", ".");

                    // if we have this file on the error list skip it
                    if (errorList.Contains(filename))
                    {
                        continue;
                    }

                    // check if we have the file in our cache
                    if (!fileCache.ContainsKey(filename))
                    {
                        // load strings from file if file exists 
                        if (File.Exists(filename))
                        {
                            try
                            {
                                using (StreamReader fileStream = File.OpenText(filename))
                                {
                                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(fileStream.ReadToEnd());
                                    if (dict == null)
                                    {
                                        dict = new Dictionary<string, string>();
                                    }
                                    fileCache.Add(filename, dict);
                                }
                            }
                            catch(Exception ex)
                            {
                                if (resultDelegate != null)
                                {
                                    ResourceStorageOperationResultItem resultItem = new ResourceStorageOperationResultItem(filename);
                                    resultItem.ProjectName = projectName;
                                    resultItem.Result = ResourceStorageOperationResult.Error;
                                    resultItem.Message = ex.GetBaseException().Message;
                                    resultDelegate(resultItem);
                                }

                                errorList.Add(filename);

                                continue;
                            }
                        }
                        else
                        {
                            // create dictionary for new file
                            var dict = new Dictionary<string, string>();
                            fileCache.Add(filename, dict);
                        }
                    }

                    // update the string
                    var stringDictionary = fileCache[filename];
                    stringDictionary[resString.Name] = resString.GetLocaleText(locale);
                }
            }

            // save all dictionaries in cache
            foreach (var item in fileCache)
            {
                ResourceStorageOperationResultItem resultItem = new ResourceStorageOperationResultItem(item.Key);
                resultItem.ProjectName = projectName;

                try
                {
                    // serialize the JSON file
                    using (StreamWriter fileStream = File.CreateText(item.Key))
                    {
                        fileStream.Write(JsonConvert.SerializeObject(item.Value, Formatting.Indented));

                        // report success
                        resultDelegate?.Invoke(resultItem);
                    }
                }
                catch (Exception ex)
                {
                    // report error
                    if (resultDelegate != null)
                    {
                        resultItem.Result = ResourceStorageOperationResult.Error;
                        resultItem.Message = ex.GetBaseException().Message;
                        resultDelegate(resultItem);
                    }
                }
            }
        }

        /// <summary>
        /// Called by Babylon.NET when synchronizing a project with the respective resource files.
        /// </summary>
        /// <param name="projectName">Name of the project whose resources are exported.</param>
        /// <returns></returns>
        public ICollection<StringResource> ImportResourceStrings(string projectName)
        {
            // We use a Dictionary to keep a list of all StringResource object searchable by the key.
            Dictionary<string, StringResource> workingDictionary = new Dictionary<string, StringResource>();

            // convert relative storage location into absolute one
            string baseDirectory = GetBaseDirectory();

            // iterate over the whole folder tree starting from the base directory.
            foreach (var file in Directory.EnumerateFiles(baseDirectory, "*.json", SearchOption.AllDirectories))
            {
                // get locale from file name
                string locale = Path.GetExtension(Path.GetFileNameWithoutExtension(file)).TrimStart(new char[] { '.' });

                using (StreamReader fileStream = File.OpenText(file))
                {
                    Dictionary<string, string> stringDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(fileStream.ReadToEnd());
                    foreach (var item in stringDictionary)
                    {
                        StringResource stringRes;
                        string relativeDirectory = Path.GetDirectoryName(file).Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                        string plainFilename = Path.Combine(relativeDirectory,  Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(Path.GetFileName(file))));

                        // check whether we already have the string
                        if (!workingDictionary.TryGetValue(plainFilename + item.Key, out stringRes))
                        {
                            stringRes = new StringResource(item.Key, "");
                            stringRes.StorageLocation = plainFilename;
                            workingDictionary.Add(plainFilename + item.Key, stringRes);
                        }

                        // add locale text. Babylon.NET uses an empty string as locale for the invariant language. A StringResource is only valid if the invariant language is set. 
                        // StringResources without an invariant language text are discared by Babylon.NET.
                        stringRes.SetLocaleText(locale, item.Value);                        
                    }
                }
            }

            // get collection of stringResources
            List<StringResource> result = new List<StringResource>();
            workingDictionary.ToList().ForEach(i => result.Add(i.Value));
            return result;
        }

        private string GetBaseDirectory()
        {
            string baseDirectory = _storageLocation;
            if (!Path.IsPathRooted(baseDirectory))
            {
                baseDirectory = Path.GetFullPath(Path.Combine(SolutionPath, baseDirectory));
            }

            return baseDirectory;
        }
    }
}
