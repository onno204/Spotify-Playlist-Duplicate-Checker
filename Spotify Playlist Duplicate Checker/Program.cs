using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;

namespace Spotify_Playlist_Duplicate_Checker {
    class Program {
        private static string _clientId = ""; //"";
        private static string _secretId = ""; //"";

        public static SpotifyWebAPI api;

        // ReSharper disable once UnusedParameter.Local
        static void Main(string[] args) {
            _clientId = string.IsNullOrEmpty(_clientId) ? Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") : _clientId;
            _secretId = string.IsNullOrEmpty(_secretId) ? Environment.GetEnvironmentVariable("SPOTIFY_SECRET_ID") : _secretId;
            Console.WriteLine("### Spotify API Duplicate checker ###");

            AuthorizationCodeAuth auth = new AuthorizationCodeAuth(_clientId, _secretId, "http://localhost:4002", "http://localhost:4002", Scope.UserTopRead | Scope.UserReadRecentlyPlayed | Scope.PlaylistReadPrivate | Scope.PlaylistReadCollaborative);
            auth.AuthReceived += AuthOnAuthReceived;
            auth.Start();
            auth.OpenBrowser();
            int counter = 0;
            while (api == null) {
                counter++;
                Console.Write(counter%3 == 0 ? "." : "\n.");
                Thread.Sleep(400);
            }
            PrintUsefulData();
            Console.ReadLine();
            auth.Stop(0);
        }

        private static async void AuthOnAuthReceived(object sender, AuthorizationCode payload) {
            AuthorizationCodeAuth auth = (AuthorizationCodeAuth)sender;
            auth.Stop();
            Token token = await auth.ExchangeCode(payload.Code);
            api = new SpotifyWebAPI {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };
        }

        private static void PrintUsefulData() {
            PrivateProfile profile = api.GetPrivateProfile();
            string name = string.IsNullOrEmpty(profile.DisplayName) ? profile.Id : profile.DisplayName;
            Console.WriteLine($"Hello there, {name}!");
            Console.WriteLine("Your playlists:");
            Paging<SimplePlaylist> playlists = api.GetUserPlaylists(profile.Id);
            do {
                playlists.Items.ForEach(playlist => {
                    Console.WriteLine($"- ({playlist.Id}){playlist.Name}");
                });
                playlists = api.GetNextPage(playlists);
            } while(playlists.HasNextPage());
            playlists = api.GetUserPlaylists(profile.Id);

            Console.Write("Enter playlist name or ID: ");
            string playlistName = Console.ReadLine();
            Console.WriteLine($"Look at playlist: {playlistName}");
            List<trackObj> processedTracks = new List<trackObj>();
            do {
                playlists.Items.ForEach(playlist => {
                    if(playlist.Name == playlistName || (playlist.Id == playlistName)) {
                        Console.WriteLine($"Checking playlist: {playlist.Name}");
                        Paging<PlaylistTrack> PTracks = api.GetPlaylistTracks(playlist.Id, "", 100, 0);
                        int counter = 0;
                        List<FullTrack> tracks = new List<FullTrack>();
                        while(counter < PTracks.Total) {
                            PTracks.Items.ForEach(tr => {
                                tracks.Add(tr.Track);
                            });
                            counter += PTracks.Items.Count;
                            PTracks = api.GetPlaylistTracks(playlist.Id, "", 100, counter);
                        }
                        Console.WriteLine("tracks.Total: " + PTracks.Total);
                        tracks.ForEach(track => {
                            trackObj tO = new trackObj {
                                albumId = track.Album.Id,
                                artists = track.Artists,
                                artistsNames = track.Artists.Select(r => r.Name).ToList(),
                                duration = track.DurationMs,
                                id = track.Id,
                                name = track.Name.Trim(),
                                actualName = getActualSongName(track.Name.Trim()).str,
                                isRemix = getActualSongName(track.Name.Trim()).bol
                            };
                            

                            processedTracks.ForEach(pTrack => {
                                double distance = JaroWinklerDistance.proximity(tO.actualName, pTrack.actualName);
                                if(distance >= 0.9) {
                                    pTrack.dupList.Add(new trackDistance {
                                        distance = distance,
                                        track = tO
                                    });
                                    tO.dupList.Add(new trackDistance {
                                        distance = distance,
                                        track = pTrack
                                    });
                                }
                            });
                            processedTracks.Add(tO);
                        });

                        processedTracks.ForEach(pTrack => {
                            if(pTrack.dupList.Count >= 1) {
                                pTrack.dupList = pTrack.dupList.OrderBy(o => o.distance).ToList();
                                string joinedString = string.Join(", ", pTrack.dupList);
                                Console.WriteLine(pTrack.name + ": " + joinedString);
                            }
                        });
                    }
                });
                playlists = api.GetNextPage(playlists);
            } while(playlists.HasNextPage());
        }




        public static stringBoolObj getActualSongName(string name) {
            // Remaster, Remastered, remix, mix, rerecored, Radio Edit, single edit, vocal edit
            List<string> nonOriginalStrings = new List<string> { "remaster", "mix", "rerecored", "edit" };
            string[] splitted = name.Split('-');
            string actualN = name;
            Boolean isRemix = false;
            for(int i = splitted.Length - 1;i >= 0;i--) {
                if(!nonOriginalStrings.Any(s => splitted[i].Contains(s))) {
                    actualN = splitted[i];
                } else {
                    isRemix = true;
                }
            }
            return new stringBoolObj{str = actualN.Trim(), bol = isRemix };
        }
    }
    public class stringBoolObj {
        public Boolean bol { get; set; }
        public string str { get; set; }
    }
    public class trackDistance {
        public trackObj track { get; set; }
        public double distance { get; set; }
        public override string ToString() {
            return track.name + "("+distance+")";
        }
    }
    public class trackObj {
        public string id { get; set; }
        public string name { get; set; }
        public string albumId { get; set; }
        public List<SimpleArtist> artists { get; set; }
        public List<String> artistsNames { get; set; }
        public int duration { get; set; }

        //After initialize
        public Boolean isRemix { get; set; }
        public string actualName { get; set; }
        public List<trackDistance> dupList { get; set; } = new List<trackDistance>();
    }

    /// <summary>
    /// https://stackoverflow.com/questions/19123506/jaro-winkler-distance-algorithm-in-c-sharp
    /// </summary>
    public static class JaroWinklerDistance {
        /* The Winkler modification will not be applied unless the 
         * percent match was at or above the mWeightThreshold percent 
         * without the modification. 
         * Winkler's paper used a default value of 0.7
         */
        private static readonly double mWeightThreshold = 0.7;

        /* Size of the prefix to be concidered by the Winkler modification. 
         * Winkler's paper used a default value of 4
         */
        private static readonly int mNumChars = 4;


        /// <summary>
        /// Returns the Jaro-Winkler distance between the specified  
        /// strings. The distance is symmetric and will fall in the 
        /// range 0 (perfect match) to 1 (no match). 
        /// </summary>
        /// <param name="aString1">First String</param>
        /// <param name="aString2">Second String</param>
        /// <returns></returns>
        public static double distance(string aString1, string aString2) {
            return 1.0 - proximity(aString1, aString2);
        }


        /// <summary>
        /// Returns the Jaro-Winkler distance between the specified  
        /// strings. The distance is symmetric and will fall in the 
        /// range 0 (no match) to 1 (perfect match). 
        /// </summary>
        /// <param name="aString1">First String</param>
        /// <param name="aString2">Second String</param>
        /// <returns></returns>
        public static double proximity(string aString1, string aString2) {
            int lLen1 = aString1.Length;
            int lLen2 = aString2.Length;
            if(lLen1 == 0)
                return lLen2 == 0 ? 1.0 : 0.0;

            int lSearchRange = Math.Max(0, Math.Max(lLen1, lLen2) / 2 - 1);

            // default initialized to false
            bool[] lMatched1 = new bool[lLen1];
            bool[] lMatched2 = new bool[lLen2];

            int lNumCommon = 0;
            for(int i = 0;i < lLen1;++i) {
                int lStart = Math.Max(0, i - lSearchRange);
                int lEnd = Math.Min(i + lSearchRange + 1, lLen2);
                for(int j = lStart;j < lEnd;++j) {
                    if(lMatched2[j])
                        continue;
                    if(aString1[i] != aString2[j])
                        continue;
                    lMatched1[i] = true;
                    lMatched2[j] = true;
                    ++lNumCommon;
                    break;
                }
            }
            if(lNumCommon == 0)
                return 0.0;

            int lNumHalfTransposed = 0;
            int k = 0;
            for(int i = 0;i < lLen1;++i) {
                if(!lMatched1[i])
                    continue;
                while(!lMatched2[k])
                    ++k;
                if(aString1[i] != aString2[k])
                    ++lNumHalfTransposed;
                ++k;
            }
            // System.Diagnostics.Debug.WriteLine("numHalfTransposed=" + numHalfTransposed);
            int lNumTransposed = lNumHalfTransposed / 2;

            // System.Diagnostics.Debug.WriteLine("numCommon=" + numCommon + " numTransposed=" + numTransposed);
            double lNumCommonD = lNumCommon;
            double lWeight = (lNumCommonD / lLen1
                             + lNumCommonD / lLen2
                             + (lNumCommon - lNumTransposed) / lNumCommonD) / 3.0;

            if(lWeight <= mWeightThreshold)
                return lWeight;
            int lMax = Math.Min(mNumChars, Math.Min(aString1.Length, aString2.Length));
            int lPos = 0;
            while(lPos < lMax && aString1[lPos] == aString2[lPos])
                ++lPos;
            if(lPos == 0)
                return lWeight;
            return lWeight + 0.1 * lPos * (1.0 - lWeight);

        }


    }



}
