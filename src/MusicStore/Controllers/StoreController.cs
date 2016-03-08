using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MusicStore.Models;
using Microsoft.Extensions.Options;


namespace MusicStore.Controllers
{
    public class StoreController : Controller
    {
        public StoreController(MusicStoreContext dbContext, IOptions<AppSettings> appSettings)
        {
            DbContext = dbContext;
            AppSettings = appSettings;
        }

        public MusicStoreContext DbContext { get; }
        private IOptions<AppSettings> AppSettings;

        //
        // GET: /Store/
        public async Task<IActionResult> Index()
        {
            var genres = await DbContext.Genres.ToListAsync();

            return View(genres);
        }

        //
        // GET: /Store/Browse?genre=Disco
        public async Task<IActionResult> Browse(string genre)
        {
            // Retrieve Genre genre and its Associated associated Albums albums from database
            var genreModel = await DbContext.Genres
                .Include(g => g.Albums)
                .Where(g => g.Name == genre)
                .FirstOrDefaultAsync();

            if (genreModel == null)
            {
                return NotFound();
            }

            return View(genreModel);
        }

        public async Task<IActionResult> Details(
            [FromServices] IMemoryCache cache,
            int id)
        {
            var cacheKey = string.Format("album_{0}", id);
            Album album;

            if (AppSettings.Value.CacheTimeout > 0)
            {
                if (!cache.TryGetValue(cacheKey, out album))
                {
                    album = await DbContext.Albums
                                    .Where(a => a.AlbumId == id)
                                    .Include(a => a.Artist)
                                    .Include(a => a.Genre)
                                    .FirstOrDefaultAsync();

                    if (album != null)
                    {
                        //Remove it from cache if not retrieved in last 10 minutes
                        cache.Set(
                            cacheKey,
                            album,
                            new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(AppSettings.Value.CacheTimeout)));
                    }
                }
            }
            else
            {
                album = await DbContext.Albums
                                .Where(a => a.AlbumId == id)
                                .Include(a => a.Artist)
                                .Include(a => a.Genre)
                                .FirstOrDefaultAsync();
            }

            if (album == null)
            {
                return NotFound();
            }

            return View(album);
        }
    }
}