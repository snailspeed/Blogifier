﻿using System.Threading.Tasks;
using Blogifier.Core.Common;
using Blogifier.Core.Data.Domain;
using Blogifier.Core.Data.Interfaces;
using Blogifier.Core.Data.Models;
using Blogifier.Core.Extensions;
using Blogifier.Core.Services.FileSystem;
using Blogifier.Core.Services.Syndication.Rss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Blogifier.Core.Controllers
{
	[Authorize]
	[Route("admin")]
	public class AdminController : Controller
	{
		private readonly string _theme;
        private readonly ILogger _logger;
        IUnitOfWork _db;
		IRssService _rss;

		public AdminController(IUnitOfWork db, IRssService rss, ILogger<AdminController> logger)
		{
			_db = db;
			_rss = rss;
            _logger = logger;
			_theme = "~/Views/Blogifier/Themes/Admin/Standard/";
		}

		public IActionResult Index()
		{
            var profile = GetProfile();

            if(profile == null)
                return RedirectToAction("Profile", "Admin");

            var posts = _db.BlogPosts.All();
			var model = new AdminPostsModel { Profile = profile, BlogPosts = posts };

            _logger.LogInformation("info test message");
            _logger.LogWarning("warning test message");

			return View(_theme + "Index.cshtml", model);
		}

		[HttpGet]
		[Route("syndication")]
		public IActionResult Syndication()
		{
			var model = new AdminSyndicationModel { Profile = GetProfile() };
			return View(_theme + "Syndication.cshtml", model);
		}

		[HttpPost]
		[Route("syndication")]
		public async Task<IActionResult> Syndication(AdminSyndicationModel model)
		{
			model.Profile = GetProfile();

			if (model.Profile == null)
				return View("Error");

			model.ProfileId = model.Profile.Id;
			await _rss.Import(model, Url.Content("~/"));

			return RedirectToAction("Index", "Admin");
		}

		[HttpGet]
		[Route("profile")]
		public IActionResult Profile()
		{
			var profile = GetProfile();

			var storage = new BlogStorage("");

			var model = new AdminProfileModel {
                Profile = profile,
                AdminThemes = storage.GetThemes(ThemeType.Admin),
                BlogThemes = storage.GetThemes(ThemeType.Blog)
            };

			return View(_theme + "Profile.cshtml", model);
		}

		[HttpPost]
		[Route("profile")]
		public IActionResult Profile(AdminProfileModel model)
		{
			var blog = model.Profile;
            var storage = new BlogStorage("");

            blog.LastUpdated = SystemClock.Now();
            model.AdminThemes = storage.GetThemes(ThemeType.Admin);
            model.BlogThemes = storage.GetThemes(ThemeType.Blog);

            if (blog.Id == 0)
			{
				blog.Slug = BlogSlugFromTitle(blog.Title);

				if(User != null)
					blog.IdentityName = User.Identity.Name;
			}

			ModelState.Clear();
			TryValidateModel(model);

			if (ModelState.IsValid)
			{
				if (blog.Id > 0)
				{
					var dbBlog = _db.Profiles.Single(b => b.Id == blog.Id);
					blog.Title = dbBlog.Title;
					blog.Description = dbBlog.Description;
					blog.AuthorName = dbBlog.AuthorName;
					blog.AuthorEmail = dbBlog.AuthorEmail;
				}
				else
				{
					_db.Profiles.Add(blog);
				}
				_db.Complete();
				var updatedBlog = _db.Profiles.Single(b => b.IdentityName == blog.IdentityName);
				model.Profile = updatedBlog;
				return View(_theme + "Profile.cshtml", model);
			}
			return RedirectToAction("Index", "Admin");
		}

		[Route("about")]
		public IActionResult About()
		{
			return View(_theme + "About.cshtml", new AdminBaseModel { Profile = GetProfile() });
		}

		#region Private members

		private Profile GetProfile()
		{
			return _db.Profiles.Single(b => b.IdentityName == User.Identity.Name);
		}

		private string BlogSlugFromTitle(string title)
		{
			var slug = title.ToSlug();
			if (_db.Profiles.Single(b => b.Slug == slug) != null)
			{
				for (int i = 2; i < 100; i++)
				{
					if (_db.Profiles.Single(b => b.Slug == slug + i.ToString()) == null)
					{
						return slug + i.ToString();
					}
				}
			}
			return slug;
		}
		
        #endregion
	}
}