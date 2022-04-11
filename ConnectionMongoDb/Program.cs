using MongoDB.Bson;
using MongoDB.Driver;

using System;

namespace ConnectionMongoDb
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var client = new MongoClient("mongodb+srv://rideit:k2GWIZywBklhOguH@ride-it.o4pze.mongodb.net/myFirstDatabase?retryWrites=true&w=majority");

            var db = client.GetDatabase("sample_mflix");

            var collection = db.GetCollection<BsonDocument>("movies");

            var result = collection.Find(new BsonDocument()).SortByDescending(m => m["runtime"]).Limit(10).ToList();

            foreach (var movie in result)
            {
                Console.WriteLine(movie.GetValue("title"));
            }
        }
    }
}