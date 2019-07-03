﻿/***

   Copyright (C) 2018-2019. dc-koromo. All Rights Reserved.
   
   Author: Koromo Copy Developer

***/

using Koromo_Copy.Component.Hitomi;
using Koromo_Copy_UX.Domain;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Koromo_Copy_UX.Utility
{
    /// <summary>
    /// BookmarkPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BookmarkPage : UserControl
    {
        string classify_name;

        public BookmarkPage(string classify_name)
        {
            InitializeComponent();

            Path.Text = classify_name.Replace("/", " / ");
            this.classify_name = classify_name;

            DataContext = new BookmarkPageDataGridViewModel();
            TagList.Sorting += new DataGridSortingEventHandler(new DataGridSorter<BookmarkPageDataGridItemViewModel>(TagList).SortHandler);

            Loaded += BookmarkPage_Loaded;
        }

        private void BookmarkPage_Loaded(object sender, RoutedEventArgs e)
        {
            load();
        }
        
        private void DataGridRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tags = TagList.SelectedItems.OfType<BookmarkPageDataGridItemViewModel>();
            if (tags.Count() > 0)
            {
                if (tags.ElementAt(0).유형 == "작품" && (Window.GetWindow(this) as Bookmark).LastLoaded != tags.ElementAt(0).내용.Split('-')[0].Trim())
                    (Window.GetWindow(this) as Bookmark).open_picturebox(tags.ElementAt(0).내용.Split('-')[0].Trim());
            }
        }

        private void TagList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tags = TagList.SelectedItems.OfType<BookmarkPageDataGridItemViewModel>();
            if (tags.Count() > 0)
            {
                Process.Start(tags.ElementAt(0).경로);
            }
        }
        
        private async void TagList_KeyDown(object sender, KeyEventArgs e)
        {
            if (TagList.SelectedItems.Count > 0)
            {
                if (e.Key == Key.Delete)
                {
                    var list = TagList.SelectedItems.OfType<BookmarkPageDataGridItemViewModel>().ToList();
                    var tldx = TagList.DataContext as BookmarkPageDataGridViewModel;

                    var dialog = new BookmarkMessage($"{list.Count}개 북마크를 삭제할까요?");
                    if ((bool)(await DialogHost.Show(dialog, "BookmarkDialog")))
                    {
                        foreach (var ll in list)
                        {
                            List<Tuple<string,BookmarkItemModel>> rl;
                            if (ll.유형 == "작가")
                                rl = BookmarkModelManager.Instance.Model.artists;
                            else if (ll.유형 == "그룹")
                                rl = BookmarkModelManager.Instance.Model.groups;
                            else
                                rl = BookmarkModelManager.Instance.Model.articles;

                            for (int i = 0; i < rl.Count; i++)
                            {
                                if (rl[i].Item1 == classify_name)
                                    if (rl[i].Item2.path == ll.경로 && rl[i].Item2.stamp.ToString() == ll.추가된날짜.ToString())
                                    {
                                        rl.RemoveAt(i);
                                        BookmarkModelManager.Instance.Save();
                                        break;
                                    }
                            }
                        }

                        load();
                    }
                }
            }
        }

        private void load()
        {
            var vm = DataContext as BookmarkPageDataGridViewModel;
            vm.Items.Clear();

            var ll = new List<BookmarkPageDataGridItemViewModel>();

            foreach (var artist in BookmarkModelManager.Instance.Model.artists)
                if (artist.Item1 == classify_name)
                    ll.Add(new BookmarkPageDataGridItemViewModel { 내용 = artist.Item2.content, 유형 = "작가", 추가된날짜 = artist.Item2.stamp.ToString(), 경로 = artist.Item2.path });
            foreach (var group in BookmarkModelManager.Instance.Model.groups)
                if (group.Item1 == classify_name)
                    ll.Add(new BookmarkPageDataGridItemViewModel { 내용 = group.Item2.content, 유형 = "그룹", 추가된날짜 = group.Item2.stamp.ToString(), 경로 = group.Item2.path });
            foreach (var article in BookmarkModelManager.Instance.Model.articles)
                if (article.Item1 == classify_name)
                    ll.Add(new BookmarkPageDataGridItemViewModel { 내용 = article.Item2.content + " - " + HitomiLegalize.GetMetadataFromMagic(article.Item2.content)?.Name, 유형 = "작품", 추가된날짜 = article.Item2.stamp.ToString(), 경로 = article.Item2.path });

            ll.Sort((x, y) => SortAlgorithm.ComparePath(y.추가된날짜, x.추가된날짜));

            for (int i = 0; i < ll.Count; i++)
                ll[i].인덱스 = (i + 1).ToString();

            foreach (var item in ll)
                vm.Items.Add(item);
        }

        private async void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                //files.ToList().ForEach(x => LoadFolder(x));

                // 폴더인지 확인
                // 1. 작가/그룹 폴더인가?
                // 2. 작품 폴더인가?

                // 파일 확인
                // 1. Zip 파일인가?
                // 2. 아니면 오류

                var artists = new List<Tuple<string, string>>();
                var groups = new List<Tuple<string, string>>();
                var articles = new List<Tuple<string, string>>();
                var err = new List<Tuple<string, string>>();

                var regexs = @"^\[(\d+)\], ^\[.*?\((\d+)\).*?\], \((\d+)\)$".Split(',').ToList().Select(x => new Regex(x.Trim())).ToList();

                foreach (var file in files)
                {
                    if (Directory.Exists(file))
                    {
                        var name = System.IO.Path.GetFileName(file);
                        
                        if(Regex.IsMatch(name, @"^[\w\s\-\.]+$"))
                        {
                            name = name.Replace(".", "").ToLower();
                            if (HitomiIndex.Instance.tagdata_collection.artist.Any(x => { if (x.Tag.Replace(".", "") == name) { name = x.Tag; return true; } return false; }))
                                artists.Add(new Tuple<string, string>(name, file));
                            else if (HitomiIndex.Instance.tagdata_collection.group.Any(x => { if (x.Tag.Replace(".", "") == name) { name = x.Tag; return true; } return false; }))
                                groups.Add(new Tuple<string, string>(name, file));
                            else
                                err.Add(new Tuple<string, string>("디렉토리 - " + name, file));
                        }
                        else
                        {
                            string match = "";
                            if (regexs.Any(x => {
                                if (!x.Match(System.IO.Path.GetFileNameWithoutExtension(name)).Success) return false;
                                match = x.Match(System.IO.Path.GetFileNameWithoutExtension(name)).Groups[1].Value;
                                return true;
                            }))
                            {
                                if (HitomiLegalize.GetMetadataFromMagic(match).HasValue)
                                    articles.Add(new Tuple<string, string>(match, file));
                                else
                                    err.Add(new Tuple<string, string>("디렉토리 - " + name, file));
                            }
                            else
                                err.Add(new Tuple<string, string>("디렉토리 - " + name, file));
                        }
                    }
                    else if (System.IO.Path.GetExtension(file).ToLower() == ".zip")
                    {
                        var name = System.IO.Path.GetFileName(file);

                        string match = "";
                        if (regexs.Any(x => {
                            if (!x.Match(System.IO.Path.GetFileNameWithoutExtension(name)).Success) return false;
                            match = x.Match(System.IO.Path.GetFileNameWithoutExtension(name)).Groups[1].Value;
                            return true;
                        }))
                        {
                            if (HitomiLegalize.GetMetadataFromMagic(match).HasValue)
                                articles.Add(new Tuple<string, string>(match, file));
                            else
                                err.Add(new Tuple<string, string>("파일 - " + name, file));
                        }
                        else
                            err.Add(new Tuple<string, string>("파일 - " + name, file));
                    }
                    else
                    {
                        err.Add(new Tuple<string, string>("알 수 없는 확장자 - " + System.IO.Path.GetFileName(file), file));
                    }
                }

                var builder = new StringBuilder();

                if (artists.Count > 0)
                {
                    builder.Append("작가\r\n" + string.Join(", ", artists.Select(x => x.Item1)) + "\r\n\r\n");
                }
                if (groups.Count > 0)
                {
                    builder.Append("그룹\r\n" + string.Join(", ", groups.Select(x => x.Item1)) + "\r\n\r\n");
                }
                if (articles.Count > 0)
                {
                    builder.Append("작품\r\n" + string.Join(", ", articles.Select(x => x.Item1)) + "\r\n\r\n");
                }
                if (err.Count > 0)
                {
                    builder.Append("출처를 찾을 수 없는 항목\r\n" + string.Join("\r\n", err.Select(x => x.Item1)));
                }

                var dialog = new BookmarkAdd(builder.ToString());
                if ((bool)(await DialogHost.Show(dialog, "BookmarkDialog")))
                {
                    foreach (var artist in artists)
                        BookmarkModelManager.Instance.Model.artists.Add(new Tuple<string, BookmarkItemModel>(classify_name, new BookmarkItemModel { content = artist.Item1, path = artist.Item2, stamp = DateTime.Now }));
                    foreach (var group in groups)
                        BookmarkModelManager.Instance.Model.groups.Add(new Tuple<string, BookmarkItemModel>(classify_name, new BookmarkItemModel { content = group.Item1, path = group.Item2, stamp = DateTime.Now }));
                    foreach (var article in articles)
                        BookmarkModelManager.Instance.Model.articles.Add(new Tuple<string, BookmarkItemModel>(classify_name, new BookmarkItemModel { content = article.Item1, path = article.Item2, stamp = DateTime.Now }));
                    BookmarkModelManager.Instance.Save();

                    await Application.Current.Dispatcher.BeginInvoke(new Action(
                    delegate
                    {
                        load();
                    }));
                }
            }
        }

        List<BookmarkPageDataGridItemViewModel> selected = new List<BookmarkPageDataGridItemViewModel>();
        Point start;
        private void TagList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            selected.Clear();
            selected.AddRange(TagList.SelectedItems.Cast<BookmarkPageDataGridItemViewModel>());
            start = e.GetPosition(null);
        }

        private void TagList_MouseMove(object sender, MouseEventArgs e)
        {
            Point mpos = e.GetPosition(null);
            Vector diff = start - mpos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (TagList.SelectedItems.Count == 0)
                    return;

                foreach (var tt in selected)
                    if (!TagList.SelectedItems.Contains(tt))
                        TagList.SelectedItems.Add(tt);

                // right about here you get the file urls of the selected items.  
                // should be quite easy, if not, ask.  
                //string[] files = new String[FileView.SelectedItems.Count];
                //int ix = 0;
                //foreach (object nextSel in FileView.SelectedItems)
                //{
                //    files[ix] = "C:\\Users\\MyName\\Music\\My playlist\\" + nextSel.ToString();
                //    ++ix;
                //}
                //string dataFormat = DataFormats.FileDrop;
                //DataObject dataObject = new DataObject(dataFormat, files);
                //DragDrop.DoDragDrop(this.FileView, dataObject, DragDropEffects.Copy);

                DataObject data = new DataObject();
                data.SetData("registries", TagList.SelectedItems.Cast<BookmarkPageDataGridItemViewModel>().ToList());
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            }
        }
    }
}
