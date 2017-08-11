using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using downr.Models;
using YamlDotNet.Serialization;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Markdig;

namespace downr.Services
{
    public interface IYamlIndexer
    {
        List<Metadata> Metadata { get; set; }
        void IndexContentFiles(string contentPath);
    }

    public class DefaultYamlIndexer : IYamlIndexer
    {
        public DefaultYamlIndexer()
        {
            Metadata = new List<Metadata>();
        }

        public List<Metadata> Metadata { get; set; }

        public void IndexContentFiles(string contentPath)
        {
            var subDirectories = Directory.GetDirectories(contentPath);
            var deserializer = new Deserializer();

            foreach (var subDirectory in subDirectories)
            {
                using (var rdr = File.OpenText(
                        Path.Combine(subDirectory, "index.md")
                    ))
                {
                    // make sure the file has the header at the first line
                    var line = rdr.ReadLine();
                    if (line == "---")
                    {
                        line = rdr.ReadLine();

                        var stringBuilder = new StringBuilder();

                        // keep going until we reach the end of the header
                        while (line != "---")
                        {
                            stringBuilder.Append(line);
                            stringBuilder.Append("\n");
                            line = rdr.ReadLine();
                        }

                        var htmlContent = rdr.ReadToEnd().TrimStart('\r', '\n', '\t', ' ');
                        htmlContent = Markdig.Markdown.ToHtml(htmlContent);

                        var yaml = stringBuilder.ToString();
                        var result = deserializer.Deserialize<Dictionary<string, string>>(new StringReader(yaml));

                        // convert the dictionary into a model
                        var slug = result[Strings.MetadataNames.Slug];
                        htmlContent = FixUpImageUrls(htmlContent, slug);
                        var metadata = new Metadata
                        {
                            Slug = slug,
                            Title = result[Strings.MetadataNames.Title],
                            Author = result[Strings.MetadataNames.Author],
                            PublicationDate = DateTime.Parse(result[Strings.MetadataNames.PublicationDate]),
                            LastModified = DateTime.Parse(result[Strings.MetadataNames.LastModified]),
                            Categories = result[Strings.MetadataNames.Categories
                                                ]?.Split(',')
                                                .Select(c => c.Trim())
                                                .ToArray()
                                                ?? new string[] { },
                            Content = htmlContent
                        };

                        Metadata.Add(metadata);
                    }
                }
            }

            Metadata = Metadata.OrderByDescending(x => x.PublicationDate).ToList();
        }

        private static string FixUpImageUrls(string html, string slug)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var nodes = htmlDoc.DocumentNode.SelectNodes("//img[@src]");
            if (nodes != null)
            {
                foreach (HtmlNode node in nodes)
                {
                    var src = node.Attributes["src"].Value;
                    src = src.Replace("media/", string.Format("/posts/{0}/media/", slug));
                    node.SetAttributeValue("src", src);
                }
            }

            html = htmlDoc.DocumentNode.OuterHtml;

            return html;
        }
    }
}