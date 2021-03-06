﻿using DevBlog.Models;
using Nest;
using System;

namespace DevBlog.Services.Elasticsearch
{
    public class ElasticProvider
    {
        private const string _indexName = "blog";
        public static IElasticClient _elastic { get; set; }

        public static void Initialize()
        {
            _elastic = GetClient();

            // Create index and define the custom filters and analyzers
            _elastic.CreateIndex(_indexName, i => i
                .Settings(s => s
                    .Setting("number_of_shards", 1)
                    .Setting("number_of_replicas", 1)
                    .Analysis(a => a
                        .TokenFilters(tf => tf
                            .EdgeNGram("edge_ngrams", e => e
                                .MinGram(1)
                                .MaxGram(50)
                                .Side(EdgeNGramSide.Front)))
                            .Analyzers(analyzer => analyzer
                                .Custom("partial_text", ca => ca
                                    .Filters(new string[] { "lowercase", "edge_ngrams" })
                                    .Tokenizer("standard"))
                                .Custom("full_text", ca => ca
                                    .Filters(new string[] { "standard", "lowercase" })
                                    .Tokenizer("standard"))))));

            // Declaration of index's mappings
            _elastic.Map<PostType>(x => x
                .Index(_indexName)
                .AutoMap());
        }

        /// <summary>
        /// Inserts or updates the entity in elastic search.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">The entity.</param>
        public void InsertUpdate(PostType document)
        {
            if(document.Deleted)
            {
                _elastic.Delete<PostType>(document.Id.ToString(), i => i
                     .Index(_indexName));
            }
            else
            {
                _elastic.Index(document, i => i
                    .Id(document.Id.ToString())
                    .Index(_indexName)
                    .Type<PostType>());
            }
        }

        public ISearchResponse<PostType> Search(string[] terms)
        {
            var result = _elastic.Search<PostType>(s => s
                .Index(_indexName)
                .Type<PostType>()
                .Query(q => MakeQuery(terms)));

            return result;
        }

        private static IElasticClient GetClient()
        {
            var urlString = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(urlString).DisableDirectStreaming();

            return new Nest.ElasticClient(settings);
        }

        private QueryContainer MakeQuery(string[] terms)
        {
            QueryContainer query = null;

            foreach (var term in terms)
            {
                query |= Query<PostType>.MultiMatch(mm => mm
                    .Query(term)
                    .Type(TextQueryType.MostFields)
                    .Fields(f => f
                        .Field(ff => ff.Title)
                        .Field(ff => ff.Content)
                        .Field(ff => ff.Tags)));
            }

            return query;
        }
    }
}
