﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FubuCore;
using FubuMVC.Core.Registration;
using FubuMVC.Spark.Tokenization.Model;
using FubuMVC.Spark.Tokenization.Parsing;

namespace FubuMVC.Spark.Tokenization
{
    public class EnrichmentContext
    {
        public string FileContent { get; set; }
        public TypePool TypePool { get; set; }
        public SparkFiles SparkFiles { get; set; }
    }

    public interface ISparkFileEnricher
    {
        void Enrich(SparkFile file, EnrichmentContext context);
    }

    // TODO : Read up on 
    // http://sparkviewengine.com/documentation/master-layouts
    // http://sparkviewengine.com/documentation/viewlocations

    public class MasterPageEnricher : ISparkFileEnricher
    {
        private const string SharedFolder = "Shared";
        private readonly ISparkParser _sparkParser;

        public MasterPageEnricher(ISparkParser sparkParser)
        {
            _sparkParser = sparkParser;
        }

        public void Enrich(SparkFile file, EnrichmentContext context)
        {
            var masterName = _sparkParser.ParseMasterName(context.FileContent);

            if(masterName.IsNotEmpty())
            {
                file.Master = findClosestMaster(masterName, file, context.SparkFiles) ??
                              findInHost(masterName, context.SparkFiles);
            }

            if (file.Master == null && masterName.IsNotEmpty())
            {
                // Log -> will result in Spark compiler blowing up.
            }
        }

        private SparkFile findClosestMaster(string masterName, SparkFile file, IEnumerable<SparkFile> files)
        {
            return files
                .Where(x => x.Origin == file.Origin)
                .Where(x => x.Root == file.Root)
                .Where(x => x.Name() == masterName)
                .Where(x => x.DirectoryPath().EndsWith(SharedFolder))
                .OrderByDescending(x => x.Path)
                .FirstOrDefault();
        }

        private SparkFile findInHost(string masterName, IEnumerable<SparkFile> files)
        {
            return files
                .Where(x => x.Origin == "Host")
                .Where(x => x.Name() == masterName)
                .Where(x => x.RelativePath() == Path.Combine(x.Root, SharedFolder).PathRelativeTo(x.Root))
                .FirstOrDefault();
        }
    }

    public class NamespaceEnricher : ISparkFileEnricher
    {
        public void Enrich(SparkFile file, EnrichmentContext context)
        {
            file.Namespace = resolveNamespace(file.ViewModelType, file.Root, file.Path);            
        }

        private static string resolveNamespace(Type viewModelType, string root, string path)
        {
            //TODO: FIX THIS, INTRODUCE PROPER ALGORITHM
            if (viewModelType == null)
            {
                return null;
            }

            var ns = viewModelType.Assembly.GetName().Name;
            var relativePath = path.PathRelativeTo(root);
            var relativeNamespace = Path.GetDirectoryName(relativePath).Replace(Path.DirectorySeparatorChar, '.');

            if (relativeNamespace.Length > 0)
            {
                ns += "." + relativeNamespace;
            }

            return ns;
        }
    }
    public class ViewModelEnricher : ISparkFileEnricher
    {
        private readonly ISparkParser _sparkParser;
        public ViewModelEnricher(ISparkParser sparkParser)
        {
            _sparkParser = sparkParser;
        }

        // Log ambiguity or return "potential types" ?
        public void Enrich(SparkFile file, EnrichmentContext context)
        {
            var fullTypeName = _sparkParser.ParseViewModelTypeName(context.FileContent);
            var matchingTypes = context.TypePool.TypesWithFullName(fullTypeName);
            var type = matchingTypes.Count() == 1 ? matchingTypes.First() : null;

            file.ViewModelType = type;
        }
    }
}