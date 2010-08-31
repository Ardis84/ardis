﻿using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NMG.Core.Domain;
using NMG.Core.Fluent;
using NMG.Core.Reader;
using NMG.Core.TextFormatter;
using NMG.Core.Util;
using System;
using System.Linq;

namespace NMG.Core.Generator
{
    public class FluentGenerator : AbstractCodeGenerator
    {
        private const string TABS = "\t\t\t";
        private readonly ApplicationPreferences applicationPreferences;

        public FluentGenerator(ApplicationPreferences applicationPreferences, Table table)
            : base(
                applicationPreferences.FolderPath, applicationPreferences.TableName,
                applicationPreferences.NameSpace,
                applicationPreferences.AssemblyName, applicationPreferences.Sequence, table)
        {
            this.applicationPreferences = applicationPreferences;
            language = this.applicationPreferences.Language;
        }

        public override void Generate()
        {
            string className = tableName.GetFormattedText().MakeSingular() + "Map";
            CodeCompileUnit compileUnit = GetCompleteCompileUnit(className);
            string generateCode = GenerateCode(compileUnit, className);
            WriteToFile(generateCode, className);
        }

        public CodeCompileUnit GetCompleteCompileUnit(string className)
        {
            var codeGenerationHelper = new CodeGenerationHelper();
            CodeCompileUnit compileUnit = codeGenerationHelper.GetCodeCompileUnit(nameSpace, className);

            CodeTypeDeclaration newType = compileUnit.Namespaces[0].Types[0];

            newType.BaseTypes.Add("ClassMap<" + Table.Name.GetFormattedText().MakeSingular() + ">");

            var constructor = new CodeConstructor {Attributes = MemberAttributes.Public};
            //newType.Members.Add(constructor);
            constructor.Statements.Add(
                new CodeSnippetStatement(TABS + "Table(\"" + Table.Name + "\");"));
            constructor.Statements.Add(
                new CodeSnippetStatement(TABS + "ReadOnly();"));
            constructor.Statements.Add(new CodeSnippetStatement(TABS + "LazyLoad();"));

            //foreach(var primaryKeys in Table.PrimaryKey)
            //{
                // refactor to set primarykeytype enum and use that instead to check
            if (Table.PrimaryKey.Type == PrimaryKeyType.PrimaryKey)
                constructor.Statements.Add(GetIdMapCodeSnippetStatement(Table.PrimaryKey.Columns[0].Name, Table.PrimaryKey.Columns[0].DataType, Table.PrimaryKey.Type));
            else
                constructor.Statements.Add(GetIdMapCodeSnippetStatement(Table.PrimaryKey));
            //}

            foreach (var fk in Table.ForeignKeys)
            {
                constructor.Statements.Add(
                    new CodeSnippetStatement(string.Format(TABS + "References(x => x.{0}).Column(\"{1}\");",
                                                           fk.References.GetFormattedText().MakeSingular(), fk.Name)));
            }

            foreach(var column in Table.Columns.Where(x => x.IsPrimaryKey != true && x.IsForeignKey != true))
            {
                string columnMapping = new DBColumnMapper().Map(column);
                constructor.Statements.Add(new CodeSnippetStatement(TABS + columnMapping));
            }

            foreach (var hasMany in Table.HasManyRelationships)
            {
                constructor.Statements.Add(
                    new CodeSnippetStatement(string.Format(TABS + "HasMany(x => x.{0});", hasMany.Reference.GetFormattedText().MakePlural())));
            }

            //if (Table.ForeignKeys.Count >= 1)
            //{

            //}

            //foreach (ColumnDetail columnDetail in columnDetails)
            //{
            //    //columnDetail.ColumnName = FormatColumnName(columnDetail.ColumnName);
            //    if (columnDetail.IsPrimaryKey)
            //    {
            //        constructor.Statements.Add(GetIdMapCodeSnippetStatement(columnDetail.ColumnName, columnDetail.DataType));
            //        IMetadataReader metadataReader = MetadataFactory.GetReader(applicationPreferences.ServerType,
            //                                                                   applicationPreferences.ConnectionString);
            //        List<string> foreignKeyTables = metadataReader.GetForeignKeyTables(columnDetail.PropertyName);
            //        foreach (string foreignKeyTable in foreignKeyTables)
            //        {
            //            constructor.Statements.Add(
            //                new CodeSnippetStatement(string.Format(TABS + "HasMany(x => x.{0}).KeyColumn(\"{1}\");",
            //                                                       foreignKeyTable.GetFormattedText() + "s", 
            //                                                       columnDetail.ColumnName)));
            //        }
            //        continue;
            //    }
            //    if (columnDetail.IsForeignKey)
            //    {
            //        constructor.Statements.Add(
            //            new CodeSnippetStatement(string.Format(TABS + "References(x => x.{0}).Column(\"{1}\");",
            //                                                   columnDetail.ForeignKeyEntity.GetFormattedText(), columnDetail.ColumnName)));
            //    }
            //    else
            //    {
            //        string columnMapping = new DBColumnMapper().Map(columnDetail);
            //        constructor.Statements.Add(new CodeSnippetStatement(TABS + columnMapping));
            //    }
            //}
            newType.Members.Add(constructor);
            return compileUnit;
        }

        private string FormatColumnName(string p)
        {
            p = p.ToLower();

            string[] columnNameParts = p.Split(new[] {'_'});
            var columnNameBuilder = new StringBuilder();

            foreach (string columnNamePart in columnNameParts)
            {
                columnNameBuilder.Append(columnNamePart.Capitalize());
            }

            return columnNameBuilder.ToString();
        }

        protected override string AddStandardHeader(string entireContent)
        {
            entireContent = "using FluentNHibernate.Mapping;" + entireContent;
            return base.AddStandardHeader(entireContent);
        }

        private static CodeSnippetStatement GetIdMapCodeSnippetStatement(string pkColumnName, string pkColumnType, PrimaryKeyType keyType)
        {
            var dataTypeMapper = new DataTypeMapper();
            bool isPkTypeIntegral = (dataTypeMapper.MapFromDBType(pkColumnType, null, null, null)).IsTypeIntegral();
            var idGeneratorType = (isPkTypeIntegral ? "GeneratedBy.Identity()" : "GeneratedBy.Assigned()");
            return
                new CodeSnippetStatement(string.Format("\t\t\tId(x => x.{0}).{1}.Column(\"{2}\");",
                                                       pkColumnName.GetFormattedText(), 
                                                       idGeneratorType,
                                                       pkColumnName));
        }

        // Generate composite key 
        //IList<Column> pkColumns, PrimaryKeyType keyType
        private static CodeSnippetStatement GetIdMapCodeSnippetStatement(PrimaryKey primaryKey)
        {
            var dataTypeMapper = new DataTypeMapper();
            //bool isPkTypeIntegral = (dataTypeMapper.MapFromDBType(pkColumnType, null, null, null)).IsTypeIntegral();
            //var idGeneratorType = (isPkTypeIntegral ? "GeneratedBy.Identity()" : "GeneratedBy.Assigned()");
            var keyPropertyBuilder = new StringBuilder(primaryKey.Columns.Count);
            foreach (var pkColumn in primaryKey.Columns)
            {
                keyPropertyBuilder.Append(String.Format(".KeyProperty(x => x.{0})", pkColumn.Name.GetFormattedText()));
            }

            return
                new CodeSnippetStatement(TABS + string.Format("CompositeId(){0};", keyPropertyBuilder));
        }

        // Generate id sequence
        private static CodeSnippetStatement GetIdMapCodeSnippetStatementSequenceId(PrimaryKeyType primaryKey)
        {

            return new CodeSnippetStatement(TABS);
        }
    }

    public static class DataTypeExtensions
    {
        public static bool IsTypeIntegral(this Type typeToCheck)
        {
            return
                typeToCheck == typeof (int) ||
                typeToCheck == typeof (long) ||
                typeToCheck == typeof (uint) ||
                typeToCheck == typeof (ulong);
        }
    }

    public static class StringExtensions
    {
        public static string Capitalize(this string inputString)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(inputString);
        }
    }
}