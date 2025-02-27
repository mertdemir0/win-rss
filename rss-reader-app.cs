using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace SimpleRSSReader
{
    public partial class MainWindow : Window
    {
        private List<Feed> feeds = new List<Feed>();
        private List<Article> articles = new List<Article>();
        private readonly string feedsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleRSSReader", "feeds.json");
        private readonly string offlineArticlesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleRSSReader", "OfflineArticles");

        public MainWindow()
        {
            InitializeComponent();
            InitializeFolders();
            LoadFeeds();
            FeedsList.ItemsSource = feeds;
        }

        private void InitializeFolders()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(feedsFilePath));
            Directory.CreateDirectory(offlineArticlesFolder);
        }

        private void LoadFeeds()
        {
            if (File.Exists(feedsFilePath))
            {
                string json = File.ReadAllText(feedsFilePath);
                feeds = JsonConvert.DeserializeObject<List<Feed>>(json) ?? new List<Feed>();
            }
        }

        private void SaveFeeds()
        {
            string json = JsonConvert.SerializeObject(feeds);
            File.WriteAllText(feedsFilePath, json);
        }

        private async void AddFeedButton_Click(object sender, RoutedEventArgs e)
        {
            string url = FeedUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please enter a RSS feed URL.");
                return;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    string feedContent = await client.GetStringAsync(url);
                    using (var reader = XmlReader.Create(new StringReader(feedContent)))
                    {
                        var feed = SyndicationFeed.Load(reader);
                        var newFeed = new Feed
                        {
                            Title = feed.Title.Text,
                            Url = url
                        };

                        if (!feeds.Any(f => f.Url == url))
                        {
                            feeds.Add(newFeed);
                            SaveFeeds();
                            FeedsList.ItemsSource = null;
                            FeedsList.ItemsSource = feeds;
                            FeedUrlTextBox.Clear();
                        }
                        else
                        {
                            MessageBox.Show("This feed already exists in your list.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding feed: {ex.Message}");
            }
        }

        private async void FeedsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FeedsList.SelectedItem is Feed selectedFeed)
            {
                await LoadArticles(selectedFeed);
            }
        }

        private async Task LoadArticles(Feed feed)
        {
            articles.Clear();
            try
            {
                using (var client = new HttpClient())
                {
                    string feedContent = await client.GetStringAsync(feed.Url);
                    using (var reader = XmlReader.Create(new StringReader(feedContent)))
                    {
                        var syndicationFeed = SyndicationFeed.Load(reader);
                        foreach (var item in syndicationFeed.Items)
                        {
                            var article = new Article
                            {
                                Title = item.Title.Text,
                                PublishDate = item.PublishDate.DateTime,
                                Link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "",
                                Summary = item.Summary?.Text ?? "",
                                IsOfflineSaved = IsArticleSavedOffline(item.Id)
                            };
                            articles.Add(article);
                        }
                    }
                }

                ArticlesList.ItemsSource = null;
                ArticlesList.ItemsSource = articles;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading articles: {ex.Message}");
            }
        }

        private bool IsArticleSavedOffline(string articleId)
        {
            string sanitizedId = SanitizeFileName(articleId);
            return File.Exists(Path.Combine(offlineArticlesFolder, sanitizedId + ".html"));
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private async void ArticlesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArticlesList.SelectedItem is Article selectedArticle)
            {
                string contentHtml;
                
                if (selectedArticle.IsOfflineSaved)
                {
                    string sanitizedId = SanitizeFileName(selectedArticle.Link);
                    contentHtml = File.ReadAllText(Path.Combine(offlineArticlesFolder, sanitizedId + ".html"));
                    OfflineStatusText.Text = "Article available offline";
                    SaveOfflineButton.Content = "Delete Offline Copy";
                }
                else
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            contentHtml = await client.GetStringAsync(selectedArticle.Link);
                        }
                        OfflineStatusText.Text = "Article not saved offline";
                        SaveOfflineButton.Content = "Save Offline";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading article content: {ex.Message}");
                        return;
                    }
                }

                // Basic HTML cleaning with HtmlAgilityPack
                var doc = new HtmlDocument();
                doc.LoadHtml(contentHtml);

                // Convert HTML to FlowDocument
                FlowDocument flowDoc = ConvertHtmlToFlowDocument(doc);
                ArticleViewer.Document = flowDoc;
            }
        }

        private FlowDocument ConvertHtmlToFlowDocument(HtmlDocument htmlDoc)
        {
            var flowDoc = new FlowDocument();
            flowDoc.FontFamily = new FontFamily("Segoe UI");
            flowDoc.FontSize = 14;

            var mainContent = htmlDoc.DocumentNode.SelectSingleNode("//article") ?? 
                              htmlDoc.DocumentNode.SelectSingleNode("//main") ?? 
                              htmlDoc.DocumentNode.SelectSingleNode("//div[@class='content']") ?? 
                              htmlDoc.DocumentNode.SelectSingleNode("//body");

            if (mainContent != null)
            {
                // Simple conversion focusing on text content
                Paragraph para = new Paragraph();
                
                // Extract title
                var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//h1") ?? 
                                htmlDoc.DocumentNode.SelectSingleNode("//title");
                if (titleNode != null)
                {
                    var titleRun = new Run(titleNode.InnerText.Trim()) { FontSize = 24, FontWeight = FontWeights.Bold };
                    para.Inlines.Add(titleRun);
                    para.Inlines.Add(new LineBreak());
                    para.Inlines.Add(new LineBreak());
                }

                // Extract paragraphs
                var paragraphs = mainContent.SelectNodes(".//p");
                if (paragraphs != null)
                {
                    foreach (var p in paragraphs)
                    {
                        para.Inlines.Add(new Run(p.InnerText.Trim()));
                        para.Inlines.Add(new LineBreak());
                        para.Inlines.Add(new LineBreak());
                    }
                }
                else
                {
                    // Fallback if no paragraphs found
                    para.Inlines.Add(new Run(mainContent.InnerText.Trim()));
                }

                flowDoc.Blocks.Add(para);
            }
            else
            {
                var para = new Paragraph(new Run("Could not extract meaningful content from this article."));
                flowDoc.Blocks.Add(para);
            }

            return flowDoc;
        }

        private async void SaveOfflineButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArticlesList.SelectedItem is Article selectedArticle)
            {
                string sanitizedId = SanitizeFileName(selectedArticle.Link);
                string offlineFilePath = Path.Combine(offlineArticlesFolder, sanitizedId + ".html");

                if (selectedArticle.IsOfflineSaved)
                {
                    // Delete the offline copy
                    File.Delete(offlineFilePath);
                    selectedArticle.IsOfflineSaved = false;
                    OfflineStatusText.Text = "Article not saved offline";
                    SaveOfflineButton.Content = "Save Offline";
                }
                else
                {
                    // Save the article offline
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            string contentHtml = await client.GetStringAsync(selectedArticle.Link);
                            File.WriteAllText(offlineFilePath, contentHtml);
                            selectedArticle.IsOfflineSaved = true;
                            OfflineStatusText.Text = "Article available offline";
                            SaveOfflineButton.Content = "Delete Offline Copy";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving article offline: {ex.Message}");
                    }
                }

                // Refresh the article list to show updated offline status
                ArticlesList.ItemsSource = null;
                ArticlesList.ItemsSource = articles;
            }
        }

        private void RemoveFeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (FeedsList.SelectedItem is Feed selectedFeed)
            {
                var result = MessageBox.Show($"Are you sure you want to remove the feed '{selectedFeed.Title}'?", 
                    "Confirm Removal", MessageBoxButton.YesNo);
                
                if (result == MessageBoxResult.Yes)
                {
                    feeds.Remove(selectedFeed);
                    SaveFeeds();
                    FeedsList.ItemsSource = null;
                    FeedsList.ItemsSource = feeds;
                    articles.Clear();
                    ArticlesList.ItemsSource = null;
                }
            }
            else
            {
                MessageBox.Show("Please select a feed to remove.");
            }
        }

        private void ViewOfflineArticlesButton_Click(object sender, RoutedEventArgs e)
        {
            var offlineArticles = new List<Article>();
            
            foreach (var feed in feeds)
            {
                foreach (var article in articles)
                {
                    if (article.IsOfflineSaved)
                    {
                        offlineArticles.Add(article);
                    }
                }
            }
            
            if (offlineArticles.Count > 0)
            {
                ArticlesList.ItemsSource = offlineArticles;
            }
            else
            {
                MessageBox.Show("No offline articles saved.");
            }
        }
    }

    public class Feed
    {
        public string Title { get; set; }
        public string Url { get; set; }
        
        public override string ToString()
        {
            return Title;
        }
    }

    public class Article
    {
        public string Title { get; set; }
        public DateTime PublishDate { get; set; }
        public string Link { get; set; }
        public string Summary { get; set; }
        public bool IsOfflineSaved { get; set; }
        
        public override string ToString()
        {
            return $"{Title} ({PublishDate:d})";
        }
    }
}
