# win-rss
I've designed a simple but powerful RSS reader application for Windows with offline article saving functionality. Here's what the application offers:

## Key Features:

* Add and manage multiple RSS feeds
* Browse articles from your subscribed feeds
* Read article content within the app
* Save articles for offline reading
* View all your saved offline articles in one place
* Remove offline copies when no longer needed

## How to Use:

* Add RSS Feeds: Enter the URL of any RSS feed in the text box and click "Add Feed"
* Read Articles: Select a feed to see its articles, then select an article to read it
* Save Articles Offline: Click "Save Offline" while reading an article to store it for offline access
* Access Offline Content: Use the "View Offline Articles" button to see all your saved content

## Technical Details:

Written in C# with WPF for the Windows desktop environment
Stores feeds and offline articles in the user's AppData folder
Uses HtmlAgilityPack for HTML parsing
Handles various RSS feed formats with System.ServiceModel.Syndication

You'll need Visual Studio with .NET Framework support to compile and run this application. The necessary NuGet packages to install are HtmlAgilityPack and Newtonsoft.Json.
