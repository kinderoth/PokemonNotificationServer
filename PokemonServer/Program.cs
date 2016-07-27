using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using PushbulletSharp;
using System.Threading;

namespace PokemonServer
{
    enum PokemonMessageTypes
    {
        None,
        Pokemon,
        Pokestop,
        Gym
    }

    class Program
    {
        static readonly int GarbageCollectorMinutes = 1;

        static object encounterLockObject = new object();
        static object writeToConsoleLock = new object();
        static object taskListLock = new object();
        static object exitLock = new object();
        static Dictionary<string, double> HandledEncounters;
        static List<Task> openTasks;
        static DateTime LastCleanTime;
        static HttpListener listener;
        static bool exitHandled = false;
        static bool exiting = false;

        [STAThread]
        static void Main( string[] args )
        {
            Console.CancelKeyPress += HandleProgramTermination;

            listener = new HttpListener();
            listener.Prefixes.Add( "http://localhost:1337/pokemonz/" );
            HandledEncounters = new Dictionary<string, double>( 500 );
            LastCleanTime = DateTime.MinValue;
            openTasks = new List<Task>();
            exiting = false;

            listener.Start();
            for ( ; ; )
            {
                if ( exiting == true )
                {
                    HandleExit();
                    break;
                }

                HttpListenerContext ctx = listener.GetContext();

                if ( exiting == true )
                {
                    HandleExit();
                    break;
                }

                Task task = Task.Factory.StartNew( () =>
                    {
                        HandleRequest( ctx.Request );
                    } ).ContinueWith( t =>
                    {
                        lock ( taskListLock )
                        {
                            openTasks.Remove( t );
                        }
                    } );

                openTasks.Add( task );
            }
        }

        static void HandleRequest( HttpListenerRequest request )
        {
            PokemonPOST msg = DeserializeFromStream( request.InputStream );
            Pokemon foundPokemon = new Pokemon( msg.Message.Pokemon_ID );

            bool handlingRequest = false;
            lock ( encounterLockObject )
            {
                if ( !HandledEncounters.ContainsKey( msg.Message.Encounter_ID ) )
                {
                    handlingRequest = true;
                    HandledEncounters.Add( msg.Message.Encounter_ID, msg.Message.Disappear_Time );
                }
            }

            if ( handlingRequest == true )
            {
                lock ( writeToConsoleLock )
                {
                    Console.WriteLine( String.Format( "Notified Player of {0} - {1}", foundPokemon.Name, msg.Message.Encounter_ID ) );
                }
            }
        }

        private static PokemonPOST DeserializeFromStream( Stream stream )
        {
            var serializer = new JsonSerializer();

            using ( var sr = new StreamReader( stream ) )
            {
                using ( var jsonTextReader = new JsonTextReader( sr ) )
                {
                    return serializer.Deserialize <PokemonPOST>( jsonTextReader );
                }
            }
        }

        static void HandleProgramTermination( object sender, ConsoleCancelEventArgs e )
        {
            exiting = true;
            Console.WriteLine( "Terminating existing tasks and exiting..." );

            HandleExit();
        }

        static void HandleExit()
        {
            lock ( exitLock )
            {
                if ( exitHandled == false )
                {
                    exitHandled = true;

                    if ( openTasks != null && openTasks.Count > 0 )
                    {
                        Task.WaitAll( openTasks.ToArray() );
                        openTasks = null;
                    }

                    if ( listener != null && listener.IsListening )
                    {
                        listener.Close();
                    }
                }
            }
        }

        static void RemoveOldPokemon()
        {
            while ( exiting == false )
            {
                Thread.Sleep( 500 );

                DateTime currTime = DateTime.Now;
                TimeSpan diff = currTime - LastCleanTime;

                if ( diff.TotalMilliseconds >= new TimeSpan( 0, GarbageCollectorMinutes, 0 ).TotalMilliseconds )
                {
                    LastCleanTime = currTime;

                    Dictionary<string, double> tempDict = new Dictionary<string, double>();
                    lock ( encounterLockObject )
                    {
                        tempDict = new Dictionary<string,double>( HandledEncounters );
                    }

                    List<string> encountersToRemove = new List<string>();
                    foreach ( KeyValuePair<string,double> kv in tempDict )
                    {
                        double time = kv.Value;
                    }
                }
            }
        }
    }

    class PokemonPOST
    {
        [JsonProperty("type")]
        private string typeString { get; set; }

        public PokemonMessageTypes MessageType
        {
            get
            {
                if ( typeString != null )
                {
                    switch ( this.typeString.Trim().ToLower() )
                    {
                        case "pokemon":
                            return PokemonMessageTypes.Pokemon;

                        default:
                            throw new NotImplementedException();
                    }
                }
                else
                {
                    return PokemonMessageTypes.None;
                }
            }
        }

        [JsonProperty("message")]
        public PokemonMessage Message { get; set; }

        public PokemonPOST()
        {
            typeString = null;
        }
    }

    class PokemonMessage
    {
        [JsonProperty("encounter_id")]
        public string Encounter_ID { get; set; }

        [JsonProperty( "spawnpoint_id" )]
        public string Spawnpoint_ID { get; set; }

        [JsonProperty( "pokemon_id" )]
        public int Pokemon_ID { get; set; }

        [JsonProperty( "latitude" )]
        public double Latitude { get; set; }

        [JsonProperty( "longitude" )]
        public double Longitude { get; set; }

        [JsonProperty( "disappear_time" )]
        public double Disappear_Time { get; set; }
    }

    public class Pokemon
    {
        private static PokemonListEntry[] PokemonList =
        {
            new PokemonListEntry { Name = "Bulbasaur",      ID = 1      },
            new PokemonListEntry { Name = "Ivysaur",        ID = 2      },
            new PokemonListEntry { Name = "Venusaur",       ID = 3      },
            new PokemonListEntry { Name = "Charmander",     ID = 4      },
            new PokemonListEntry { Name = "Charmeleon",     ID = 5      },
            new PokemonListEntry { Name = "Charizard",      ID = 6      },
            new PokemonListEntry { Name = "Squirtle",       ID = 7      },
            new PokemonListEntry { Name = "Wartortle",      ID = 8      },
            new PokemonListEntry { Name = "Blastoise",      ID = 9      },
            new PokemonListEntry { Name = "Caterpie",       ID = 10     },
            new PokemonListEntry { Name = "Metapod",        ID = 11     },
            new PokemonListEntry { Name = "Butterfree",     ID = 12     },
            new PokemonListEntry { Name = "Weedle",         ID = 13     },
            new PokemonListEntry { Name = "Kakuna",         ID = 14     },
            new PokemonListEntry { Name = "Beedrill",       ID = 15     },
            new PokemonListEntry { Name = "Pidgey",         ID = 16     },
            new PokemonListEntry { Name = "Pidgeotto",      ID = 17     },
            new PokemonListEntry { Name = "Pidgeot",        ID = 18     },
            new PokemonListEntry { Name = "Rattata",        ID = 19     },
            new PokemonListEntry { Name = "Raticate",       ID = 20     },
            new PokemonListEntry { Name = "Spearow",        ID = 21     },
            new PokemonListEntry { Name = "Fearow",         ID = 22     },
            new PokemonListEntry { Name = "Ekans",          ID = 23     },
            new PokemonListEntry { Name = "Arbok",          ID = 24     },
            new PokemonListEntry { Name = "Pikachu",        ID = 25     },
            new PokemonListEntry { Name = "Raichu",         ID = 26     },
            new PokemonListEntry { Name = "Sandshrew",      ID = 27     },
            new PokemonListEntry { Name = "Sandslash",      ID = 28     },
            new PokemonListEntry { Name = "Nidoran♀",       ID = 29     },
            new PokemonListEntry { Name = "Nidorina",       ID = 30     },
            new PokemonListEntry { Name = "Nidoqueen",      ID = 31     },
            new PokemonListEntry { Name = "Nidoran♂",       ID = 32     },
            new PokemonListEntry { Name = "Nidorino",       ID = 33     },
            new PokemonListEntry { Name = "Nidoking",       ID = 34     },
            new PokemonListEntry { Name = "Clefairy",       ID = 35     },
            new PokemonListEntry { Name = "Clefable",       ID = 36     },
            new PokemonListEntry { Name = "Vulpix",         ID = 37     },
            new PokemonListEntry { Name = "Ninetales",      ID = 38     },
            new PokemonListEntry { Name = "Jigglypuff",     ID = 39     },
            new PokemonListEntry { Name = "Wigglytuff",     ID = 40     },
            new PokemonListEntry { Name = "Zubat",          ID = 41     },
            new PokemonListEntry { Name = "Golbat",         ID = 42     },
            new PokemonListEntry { Name = "Oddish",         ID = 43     },
            new PokemonListEntry { Name = "Gloom",          ID = 44     },
            new PokemonListEntry { Name = "Vileplume",      ID = 45     },
            new PokemonListEntry { Name = "Paras",          ID = 46     },
            new PokemonListEntry { Name = "Parasect",       ID = 47     },
            new PokemonListEntry { Name = "Venonat",        ID = 48     },
            new PokemonListEntry { Name = "Venomoth",       ID = 49     },
            new PokemonListEntry { Name = "Diglett",        ID = 50     },
            new PokemonListEntry { Name = "Dugtrio",        ID = 51     },
            new PokemonListEntry { Name = "Meowth",         ID = 52     },
            new PokemonListEntry { Name = "Persian",        ID = 53     },
            new PokemonListEntry { Name = "Psyduck",        ID = 54     },
            new PokemonListEntry { Name = "Golduck",        ID = 55     },
            new PokemonListEntry { Name = "Mankey",         ID = 56     },
            new PokemonListEntry { Name = "Primeape",       ID = 57     },
            new PokemonListEntry { Name = "Growlithe",      ID = 58     },
            new PokemonListEntry { Name = "Arcanine",       ID = 59     },
            new PokemonListEntry { Name = "Poliwag",        ID = 60     },
            new PokemonListEntry { Name = "Poliwhirl",      ID = 61     },
            new PokemonListEntry { Name = "Poliwrath",      ID = 62     },
            new PokemonListEntry { Name = "Abra",           ID = 63     },
            new PokemonListEntry { Name = "Kadabra",        ID = 64     },
            new PokemonListEntry { Name = "Alakazam",       ID = 65     },
            new PokemonListEntry { Name = "Machop",         ID = 66     },
            new PokemonListEntry { Name = "Machoke",        ID = 67     },
            new PokemonListEntry { Name = "Machamp",        ID = 68     },
            new PokemonListEntry { Name = "Bellsprout",     ID = 69     },
            new PokemonListEntry { Name = "Weepinbell",     ID = 70     },
            new PokemonListEntry { Name = "Victreebel",     ID = 71     },
            new PokemonListEntry { Name = "Tentacool",      ID = 72     },
            new PokemonListEntry { Name = "Tentacruel",     ID = 73     },
            new PokemonListEntry { Name = "Geodude",        ID = 74     },
            new PokemonListEntry { Name = "Graveler",       ID = 75     },
            new PokemonListEntry { Name = "Golem",          ID = 76     },
            new PokemonListEntry { Name = "Ponyta",         ID = 77     },
            new PokemonListEntry { Name = "Rapidash",       ID = 78     },
            new PokemonListEntry { Name = "Slowpoke",       ID = 79     },
            new PokemonListEntry { Name = "Slowbro",        ID = 80     },
            new PokemonListEntry { Name = "Magnemite",      ID = 81     },
            new PokemonListEntry { Name = "Magneton",       ID = 82     },
            new PokemonListEntry { Name = "Farfetch'd",     ID = 83     },
            new PokemonListEntry { Name = "Doduo",          ID = 84     },
            new PokemonListEntry { Name = "Dodrio",         ID = 85     },
            new PokemonListEntry { Name = "Seel",           ID = 86     },
            new PokemonListEntry { Name = "Dewgong",        ID = 87     },
            new PokemonListEntry { Name = "Grimer",         ID = 88     },
            new PokemonListEntry { Name = "Muk",            ID = 89     },
            new PokemonListEntry { Name = "Shellder",       ID = 90     },
            new PokemonListEntry { Name = "Cloyster",       ID = 91     },
            new PokemonListEntry { Name = "Gastly",         ID = 92     },
            new PokemonListEntry { Name = "Haunter",        ID = 93     },
            new PokemonListEntry { Name = "Gengar",         ID = 94     },
            new PokemonListEntry { Name = "Onix",           ID = 95     },
            new PokemonListEntry { Name = "Drowzee",        ID = 96     },
            new PokemonListEntry { Name = "Hypno",          ID = 97     },
            new PokemonListEntry { Name = "Krabby",         ID = 98     },
            new PokemonListEntry { Name = "Kingler",        ID = 99     },
            new PokemonListEntry { Name = "Voltorb",        ID = 100    },
            new PokemonListEntry { Name = "Electrode",      ID = 101    },
            new PokemonListEntry { Name = "Exeggcute",      ID = 102    },
            new PokemonListEntry { Name = "Exeggutor",      ID = 103    },
            new PokemonListEntry { Name = "Cubone",         ID = 104    },
            new PokemonListEntry { Name = "Marowak",        ID = 105    },
            new PokemonListEntry { Name = "Hitmonlee",      ID = 106    },
            new PokemonListEntry { Name = "Hitmonchan",     ID = 107    },
            new PokemonListEntry { Name = "Lickitung",      ID = 108    },
            new PokemonListEntry { Name = "Koffing",        ID = 109    },
            new PokemonListEntry { Name = "Weezing",        ID = 110    },
            new PokemonListEntry { Name = "Rhyhorn",        ID = 111    },
            new PokemonListEntry { Name = "Rhydon",         ID = 112    },
            new PokemonListEntry { Name = "Chansey",        ID = 113    },
            new PokemonListEntry { Name = "Tangela",        ID = 114    },
            new PokemonListEntry { Name = "Kangaskhan",     ID = 115    },
            new PokemonListEntry { Name = "Horsea",         ID = 116    },
            new PokemonListEntry { Name = "Seadra",         ID = 117    },
            new PokemonListEntry { Name = "Goldeen",        ID = 118    },
            new PokemonListEntry { Name = "Seaking",        ID = 119    },
            new PokemonListEntry { Name = "Staryu",         ID = 120    },
            new PokemonListEntry { Name = "Starmie",        ID = 121    },
            new PokemonListEntry { Name = "Mr. Mime",       ID = 122    },
            new PokemonListEntry { Name = "Scyther",        ID = 123    },
            new PokemonListEntry { Name = "Jynx",           ID = 124    },
            new PokemonListEntry { Name = "Electabuzz",     ID = 125    },
            new PokemonListEntry { Name = "Magmar",         ID = 126    },
            new PokemonListEntry { Name = "Pinsir",         ID = 127    },
            new PokemonListEntry { Name = "Tauros",         ID = 128    },
            new PokemonListEntry { Name = "Magikarp",       ID = 129    },
            new PokemonListEntry { Name = "Gyarados",       ID = 130    },
            new PokemonListEntry { Name = "Lapras",         ID = 131    },
            new PokemonListEntry { Name = "Ditto",          ID = 132    },
            new PokemonListEntry { Name = "Eevee",          ID = 133    },
            new PokemonListEntry { Name = "Vaporeon",       ID = 134    },
            new PokemonListEntry { Name = "Jolteon",        ID = 135    },
            new PokemonListEntry { Name = "Flareon",        ID = 136    },
            new PokemonListEntry { Name = "Porygon",        ID = 137    },
            new PokemonListEntry { Name = "Omanyte",        ID = 138    },
            new PokemonListEntry { Name = "Omastar",        ID = 139    },
            new PokemonListEntry { Name = "Kabuto",         ID = 140    },
            new PokemonListEntry { Name = "Kabutops",       ID = 141    },
            new PokemonListEntry { Name = "Aerodactyl",     ID = 142    },
            new PokemonListEntry { Name = "Snorlax",        ID = 143    },
            new PokemonListEntry { Name = "Articuno",       ID = 144    },
            new PokemonListEntry { Name = "Zapdos",         ID = 145    },
            new PokemonListEntry { Name = "Moltres",        ID = 146    },
            new PokemonListEntry { Name = "Dratini",        ID = 147    },
            new PokemonListEntry { Name = "Dragonair",      ID = 148    },
            new PokemonListEntry { Name = "Dragonite",      ID = 149    },
            new PokemonListEntry { Name = "Mewtwo",         ID = 150    },
            new PokemonListEntry { Name = "Mew",            ID = 151    },
        };

        public string Name { get; private set; }
        public int ID { get; private set; }

        public Pokemon( string name )
        {
            PokemonListEntry[] entries = PokemonList.Where( e => e.Name.ToLower().Equals( name.ToLower() ) ).ToArray();

            if ( entries.Length != 1 )
            {
                throw new InvalidDataException();
            }

            this.Name = entries[ 0 ].Name;
            this.ID = entries[ 0 ].ID;
        }

        public Pokemon( int ID )
        {
            PokemonListEntry[] entries = PokemonList.Where( e => e.ID == ID ).ToArray();

            if ( entries.Length != 1 )
            {
                throw new InvalidDataException();
            }

            this.Name = entries[ 0 ].Name;
            this.ID = entries[ 0 ].ID;
        }

        class PokemonListEntry
        {
            public string Name { get; set; }
            public int ID { get; set; }
        }
    }
}
