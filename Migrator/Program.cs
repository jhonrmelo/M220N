using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using M220N.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Migrator
{
    internal class Program
    {
        private static IMongoCollection<Movie> _moviesCollection;

        private static string mongoConnectionString = "";

        private static async Task Main(string[] args)
        {
            Setup();

            Console.WriteLine("Starting the data migration.");
            var datePipelineResults = TransformDatePipeline();
            Console.WriteLine($"I found {datePipelineResults.Count} docs where the lastupdated field is of type 'string'.");

            if (datePipelineResults.Count > 0)
            {
                BulkWriteResult<Movie> bulkWriteDatesResult = null;
                // TODO Ticket: Call  _moviesCollection.BulkWriteAsync, passing in the
                // datePipelineResults. You will need to use a ReplaceOneModel<Movie>
                // (https://mongodb.github.io/mongo-csharp-driver/2.12/apidocs/html/T_MongoDB_Driver_ReplaceOneModel_1.htm).
                //
                List<WriteModel<Movie>> writeModels = new List<WriteModel<Movie>>();

                foreach (var result in datePipelineResults)
                {
                    var filter = Builders<Movie>.Filter.Eq(x => x.Id, result.Id);

                    writeModels.Add(new ReplaceOneModel<Movie>(filter, result));
                }

                bulkWriteDatesResult = await _moviesCollection.BulkWriteAsync(writeModels);

                Console.WriteLine($"{bulkWriteDatesResult.ProcessedRequests.Count} records updated.");
            }

            var ratingPipelineResults = TransformRatingPipeline();
            Console.WriteLine($"I found {ratingPipelineResults.Count} docs where the imdb.rating field is not a number type.");

            if (ratingPipelineResults.Count > 0)
            {
                BulkWriteResult<Movie> bulkWriteRatingsResult = null;
                // TODO Ticket: Call  _moviesCollection.BulkWriteAsync, passing in the
                // ratingPipelineResults. You will need to use a ReplaceOneModel<Movie>
                // (https://mongodb.github.io/mongo-csharp-driver/2.12/apidocs/html/T_MongoDB_Driver_ReplaceOneModel_1.htm).
                //
                // // bulkWriteRatingsResult = await _moviesCollection.BulkWriteAsync(...

                List<WriteModel<Movie>> writeModels = new List<WriteModel<Movie>>();

                foreach (var result in ratingPipelineResults)
                {
                    var filter = Builders<Movie>.Filter.Eq(x => x.Id, result.Id);

                    writeModels.Add(new ReplaceOneModel<Movie>(filter, result));
                }

                bulkWriteRatingsResult = await _moviesCollection.BulkWriteAsync(writeModels);

                Console.WriteLine($"{bulkWriteRatingsResult.ProcessedRequests.Count} records updated.");
            }

            Console.WriteLine();
            Console.WriteLine("Checking the data conversions...");

            Verify();

            // Keep the console window open until user hits `enter` or closes.
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Press <Enter> to close.");
            Console.ReadLine();
        }

        private static void Setup()
        {
            var camelCaseConvention = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("CamelCase", camelCaseConvention, type => true);

            var mongoUri = mongoConnectionString;
            var mflixClient = new MongoClient(mongoUri);
            var moviesDatabase = mflixClient.GetDatabase("sample_mflix");
            _moviesCollection = moviesDatabase.GetCollection<Movie>("movies");
        }

        /// <summary>
        ///     Creates an aggregation pipeline that finds all documents
        ///     that have a non-numeric 'imdb.rating' value
        ///     and converts those values to type 'double'.
        ///
        ///    The code below is the C# way to represent the following pipeline:
        ///
        ///    [{'$match': {'imdb.rating': {'$not': {'$type': 'number'}}}}, {
        ///      '$addFields': {'imdb.rating': {'$convert': {
        ///          'input': 'imdb.rating', 'to': 'double', 'onError': -1}}}}]
        ///
        /// </summary>
        /// <returns>A List of Movie objects with the rating values converted.</returns>
        private static List<Movie> TransformRatingPipeline()
        {
            var pipeline = new[]
            {
                new BsonDocument("$match",
                    new BsonDocument("imdb.rating",
                        new BsonDocument("$not",
                            new BsonDocument("$type", "number")))),
                new BsonDocument("$addFields",
                    new BsonDocument("imdb.rating",
                        new BsonDocument("$convert",
                            new BsonDocument
                            {
                                {"input", "imdb.rating"},
                                {"to", "double"},
                                {"onError", -1}
                            })))
            };

            return _moviesCollection
                .Aggregate(PipelineDefinition<Movie, Movie>.Create(pipeline))
                .ToList();
        }

        /// <summary>
        ///     Creates an aggregation pipeline that finds all documents
        ///     that have a string 'lastupdated' value
        ///     and converts those values to type 'date'.
        ///
        ///    The code below is the C# way to represent the following pipeline:
        ///
        ///     [{'$match': {'lastupdated': {'$type': 2}}}, {
        ///          '$addFields': {'lastupdated': {'$dateFromString': {
        ///          'dateString': {'$substr': ['$lastupdated', 0, 23]}}}}}]
        ///
        /// </summary>
        /// <returns>A List of Movie objects with the lastupdated values converted to dates.</returns>
        private static List<Movie> TransformDatePipeline()
        {
            var pipeline = new[]
            {
                new BsonDocument("$match",
                    new BsonDocument("lastupdated",
                        new BsonDocument("$type", 2))),
                new BsonDocument("$addFields",
                    new BsonDocument("lastupdated",
                        new BsonDocument("$dateFromString",
                            new BsonDocument
                            {
                                {
                                    "dateString",
                                    new BsonDocument("$substr",
                                        new BsonArray
                                        {
                                            "$lastupdated",
                                            0,
                                            23
                                        })
                                },
                                {"timezone", "America/New_York"}
                            })))
            };

            return _moviesCollection
                .Aggregate(PipelineDefinition<Movie, Movie>.Create(pipeline))
                .ToList();
        }

        private static void Verify()
        {
            var pipeline = new[]
            {
                new BsonDocument("$match",
                    new BsonDocument("$or",
                        new BsonArray
                        {
                            new BsonDocument("lastupdated",
                                new BsonDocument("$type", "string")),
                            new BsonDocument("imdb.rating",
                                new BsonDocument("$type", "string"))
                        })),
                new BsonDocument("$count", "badDocs")
            };

            var badDocs = _moviesCollection
                .Aggregate(PipelineDefinition<Movie, BsonDocument>.Create(pipeline))
                .ToList();

            if (badDocs.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[✓] No remaining docs to be converted. Great job!");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ ] Uh oh. One or both of your pipelines missed {badDocs.Count} documents...");
            }
        }
    }
}