﻿using System;
//using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI.Popups;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;
//using Windows.ApplicationModel.Resources.Core;
//using Windows.Foundation;
//using Windows.Foundation.Collections;
//using Windows.UI.Xaml.Data;
//using Windows.UI.Xaml.Media;
//using Windows.UI.Xaml.Media.Imaging;
//using System.Collections.Specialized;
using System.Linq;
using System.Collections;
using System.Xml.Serialization;
using System.IO;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using LPF_SOAP;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Windows.UI.Xaml; // needed for WriteableBitmap.PixelBuffer.ToStream()

// This is the underlying TP data model, which is mapped to the SampleDataItem of the SampleDataSource model with its data bindings in the standard item templates.
namespace TP8.Data
{

    /// <summary>
    /// Like Win 7 TriagePic's PatientReports class
    /// </summary>
    [XmlType(TypeName = "PatientReports")]
    public class TP_PatientReports : IEnumerable<TP_PatientReport>
    {
        const string PATIENT_REPORTS_SENT_FILENAME = "PatientReportsSent.xml";

        private List<TP_PatientReport> inner = new List<TP_PatientReport>();

        public void Add(object o)
        {
            inner.Add((TP_PatientReport)o);
        }

        public void Remove(TP_PatientReport o)
        {
            inner.Remove(o);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="patientID"></param>
        /// <param name="targetVersion"></param>
        public void Discard(string patientID, int targetVersion)
        {
            int index = FindIndexByPatientIDAndSentCodeVersion(patientID, targetVersion);
            if (index >= 0)
                Remove(inner[index]);
            //return index;
        }

        public void Clear()
        {
            inner.Clear();
        }

        public List<TP_PatientReport> GetAsList()
        {
            return inner;
        }

        public void ReplaceWithList(List<TP_PatientReport> list)
        {
            inner = list;
        }

        public async Task ReadXML()
        {
            await ReadXML(PATIENT_REPORTS_SENT_FILENAME); // outbox
        }

        public async Task ReadXML(string filename)
        {
            Clear();
            await App.LocalStorageDataSemaphore.WaitAsync(); // Data buffer shared with other read/writes, so serialize access
            LocalStorage.Data.Clear();
            await LocalStorage.Restore<TP_PatientReport>(filename);
            if (LocalStorage.Data != null)
            {
                TP_PatientReport pdi__;
                foreach (var item in LocalStorage.Data)
                {
                    // Fixup:
                    pdi__ = item as TP_PatientReport;
                    if (String.IsNullOrEmpty(pdi__.PatientID))
                        continue; // Another version of Scrub().  Debug interrupts may cause null records.
                    pdi__.AdjustAfterReadXML();
                    inner.Add(pdi__);
                }
            }
            App.LocalStorageDataSemaphore.Release();
        }

        public async Task WriteXML()
        {
            await WriteXML(PATIENT_REPORTS_SENT_FILENAME);
        }

        public async Task WriteXML(string filename)
        {
            await App.LocalStorageDataSemaphore.WaitAsync(); // Data buffer shared with other read/writes, so serialize access
            LocalStorage.Data.Clear();
            TP_PatientReport pdi__;
            foreach (var patient in inner)
            {
                // Fixup:
                pdi__ = patient as TP_PatientReport;
                if (String.IsNullOrEmpty(pdi__.PatientID))
                    continue; // Another version of Scrub().   Debug interrupts may cause null records.
                pdi__.AdjustBeforeWriteXML();
                LocalStorage.Add(pdi__);
            }

            await LocalStorage.Save<TP_PatientReport>(filename);
            App.LocalStorageDataSemaphore.Release();
        }

        // Not right at compile time...
//        public TP_PatientReports OrderBy<T>(Func<TP_PatientReport, TKey> keySelector)
//        {
//            return inner.OrderBy(keySelector).ToList();
//        }

        public IEnumerator<TP_PatientReport> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void UpdateOrAdd(TP_PatientReport o)
        {
            int index = FindIndexByPatientIDAndSentCodeVersion(o.PatientID, o.ObjSentCode.GetVersionCount());
            if (index >= 0)
                Update(o, index);
            else
                Add(o);
        }


        /// <summary>
        /// See if a report with the given PatientID + Target Version (e.g., Msg #) is in the list, and if so returns the zero-based list index
        /// </summary>
        /// <param name="o"></param>
        /// <returns>-1 if not present</returns>
        public int FindIndexByPatientIDAndSentCodeVersion(string PatientID, int TargetVersion)
        {
            return inner.FindIndex(i => i.PatientID == PatientID && i.ObjSentCode.GetVersionCount() == TargetVersion); // C# 3.0 lambda expression
        }

        public void Update(TP_PatientReport o, int index)
        {
            App.MyAssert(index >= 0 && index < inner.Count());
            inner[index] = o;
        }

        public TP_PatientReport Fetch(int index)
        {
            App.MyAssert(index >= 0 && index < inner.Count());
            return inner[index];
        }

        /// <summary>
        /// WhenLocalTime can serve as unique ID
        /// </summary>
        /// <param name="when"></param>
        /// <returns></returns>
        public int FindIndexByWhenLocalTime(string when)
        {
            return inner.FindIndex(i => i.WhenLocalTime == when); // C# 3.0 lambda expression
        }

    }


    /// <summary>
    /// Creates a collection of groups and items with hard-coded content.
    /// 
    /// SampleDataSource initializes with placeholder data rather than live production
    /// data so that sample data is provided at both design-time and run-time.
    /// </summary>
    [DataContract]
    public class TP_PatientDataGroups
    {
        const string PATIENT_REPORTS_ALL_STATIONS_FILENAME = "PatientReportsAllStations.xml";

        private TP_PatientReports _outbox = new TP_PatientReports();
        private TP_PatientReports _allstations = new TP_PatientReports();
//        private TP_PatientReports _outboxfiltered = new TP_PatientReports();
//        private TP_PatientReports _allstationsfiltered = new TP_PatientReports();
        private TP_PatientReports _outboxsorted = new TP_PatientReports();
        private TP_PatientReports _allstationssorted = new TP_PatientReports();
        private TP_PatientReports _outboxsortedandfiltered = new TP_PatientReports();
        private TP_PatientReports _allstationssortedandfiltered = new TP_PatientReports();
        private TP_PatientReports _outboxforstatistics = new TP_PatientReports(); // New June 2015. WhenLocalTime is changed to consist of date only (for daily counts), no time stamp or UTC offset

        public TP_PatientReports GetOutbox()
        {
            return _outbox;
        }

        public TP_PatientReports GetAllStations()
        {
            return _allstations;
        }
/*
        public TP_PatientReports GetOutboxFiltered()
        {
            return _outboxfiltered;
        }

        public TP_PatientReports GetAllStationsFiltered()
        {
            return _allstationsfiltered;
        }
*/
        public TP_PatientReports GetOutboxSorted()
        {
            return _outboxsorted;
        }

        public TP_PatientReports GetAllStationsSorted()
        {
            return _allstationssorted;
        }

        public TP_PatientReports GetOutboxSortedAndFiltered()
        {
            return _outboxsortedandfiltered;
        }

        public TP_PatientReports GetAllStationsSortedAndFiltered()
        {
            return _allstationssortedandfiltered;
        }

        public TP_PatientReports GetOutboxForStatistics()
        {
            return _outboxforstatistics;
        }

        public TP_PatientDataGroups()
        {
            // can't await here for Init(), so call separately after this class constructed
        }

        public async Task Init()
        {
            await _outbox.ReadXML(); // Try this earlier in the process, so user can see list sooner (than waiting to Init2)
/* WAS:
            await _outbox.ReadXML();
            if(_outbox.Count() == 0)
            {
                // ProcessPatientReportsSent - Generate dummy data
                AddItemsSet1(_outbox);
                foreach (var p in _outbox)
                    p.CompleteGeneratedRecord(); // Add boilerplate values not done above
                await _outbox.WriteXML();
            }
            // Init of all stations list moved to Init2()
 */
        }

        /// <summary>
        /// Call this after Init and after current filter and App.CurrentDisaster is set up.
        /// </summary>
        public async void Init2()
        {
            if(_outbox.Count() == 0)
                await ProcessOutboxList(App.pd.plUserName, App.pd.plPassword, true); // startup is true

            // Rest here is similar to ReloadAllStationsList, but also includes Outbox
            App.ReloadingAllStationsList = true; // added v 3.5, supercedes App.ProcessingAllStationsList
            await ProcessAllStationsList(App.pd.plUserName, App.pd.plPassword, true, false); // startup is true, invalid Cache first is false (unnecessary)
            UpdateAllStationsLoadMessage(5); // Scrub, Filter, Sort. new v 3.5
/* WAS:
            await _allstations.ReadXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);
            if(_allstations.Count() == 0)
            {
                // More to come
                AddItemsSet1(_allstations);
                AddItemsSet2(_allstations);
                foreach (var p in _allstations)
                    p.CompleteGeneratedRecord(); // Add boilerplate values not done above
                await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);
            }
*/
            await Scrub(); // Remove bad data. Await added v 3.5
            //ReFilter();
            await ReSortAndFilter();  // includes OutboxForStatisticsRefresh() as of June 2015. Await added for v 3.5
            // must follow ReSortAndFilter:
            // was: await SampleDataSource.RefreshOutboxItems(); // For benefit of next peek at Outbox. Await added for v 3.5
            await SampleDataSource.RefreshOutboxAndAllStationsItems();
            UpdateAllStationsLoadMessage(0); // new here v 3.5
            App.ReloadingAllStationsList = false;
        }

        public async void UpdateListsAfterReportDelete(bool DeletedAtTriageTrakToo) // New Dec 2014
        {
            if (DeletedAtTriageTrakToo)
            {
                // We could do this quickly with existing cached list, e.g.:
                    //ReSortAndFilter(); // outbox and allstations
                    //SampleDataSource.RefreshOutboxAndAllStationsItems();
                // but maybe better to do fresh fetch:
                await ReloadAllStationsListAsync();
            }
            // Caller has already done post-delete outbox list scrub, and save to XML file.
            // Not necessary to call service for this: await ProcessOutboxList(App.pd.plUserName, App.pd.plPassword, false); // startup is false
            await ReSortAndFilterImplAsync(_outbox, _outboxsorted, _outboxsortedandfiltered, true, true); // Await added v 3.5
            OutboxForStatisticsRefresh(); // new June 2015
            await SampleDataSource.RefreshOutboxItems(); // For benefit of next peek at Outbox.  Await added for v 3.5
        }

        public async Task ReloadAllStationsListAsync()
        {
            await ReloadAllStationsListAsync(false);
        }

        public async Task ReloadAllStationsListAsync(bool invalidateCacheFirst) // parameter added v 3.5
        {
            App.ReloadingAllStationsList = true; // added v 3.5, supercedes App.ProcessingAllStationsList
            await ProcessAllStationsList(App.pd.plUserName, App.pd.plPassword, false, invalidateCacheFirst);
            UpdateAllStationsLoadMessage(5); // Scrub, Filter, Sort. new v 3.5
            await ScrubAllStations(); // await added for v 3.5
            await ReSortAndFilterImplAsync(_allstations, _allstationssorted, _allstationssortedandfiltered, false, false); // as in ReSortAndMinimallyFilter. Await added v 3.5
            await SampleDataSource.RefreshAllStationsItems(); // For benefit of next peek. Await added for v 3.5
            /* Tried following as alternative to previous line, to see if it would refresh UI.  Loop only executed once.
            int lastCount = _allstationssortedandfiltered.Count(); /// EXPERIMENT LOOP
            int thisCount = 0;
            while (true)
            {
                SampleDataSource.RefreshAllStationsItems(); // For benefit of next peek
                thisCount = _allstationssortedandfiltered.Count();
                if (lastCount == thisCount)
                    break;
                lastCount = thisCount;
            } */
            UpdateAllStationsLoadMessage(0); // move here v 3.5
            App.ReloadingAllStationsList = false; 
        }

        public async Task ReadCachedAllStationsList()
        {
            await _allstations.ReadXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME); // These results were previously filtered, so don't have to do that again.
        }

        public async Task PurgeCachedAllStationsList()
        {
            // Do this when the current event changes, cache is no longer valid
            _allstations.Clear();
            _allstationssorted.Clear();
            _allstationssortedandfiltered.Clear();
            await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME); // empty file
        }


        /// <summary>
        /// Get All Stations list from web service for current event.  Assume that in general there's already stale data available if we don't succeed here.
        /// </summary>
        /// <param name="plname"></param>
        /// <param name="plpass"></param>
        /// <returns></returns>
        /// 
        public async Task ProcessAllStationsList(string plname, string plpass) // Compare Win 7 FormTriagePic.ProcessEventList(...)
        {
           await ProcessAllStationsList(plname, plpass, false, false);
        }

        public async Task ProcessAllStationsList(string plname, string plpass, bool onStartup, bool invalidateCacheFirst)
        {
            //App.ProcessingAllStationsList = true;
            await ProcessAllStationsListImpl(plname, plpass, onStartup, invalidateCacheFirst);
            //App.ProcessingAllStationsList = false;

            if (TP8.BasicPageChecklist.staticProgressBarChangedEvent != null)
                TP8.BasicPageChecklist.staticProgressBarChangedEvent.Visibility = Visibility.Collapsed;
            /* Moved to ReloadAllStations (call) and UpdateAllStationsLoadMessag for v 3.5:
            if (TP8.BasicPageChecklist.staticGettingAllStationsReports != null)
            {
                TP8.BasicPageChecklist.staticGettingAllStationsReports.Text = ""; // cleanup
                TP8.BasicPageChecklist.staticGettingAllStationsReports.Visibility = Visibility.Collapsed;
            }*/
        }


        /// <summary>
        /// Show loading status on Checklist page of All Stations data. 
        /// </summary>
        /// <param name="step">If 0, clear & hide message box</param>
        private void UpdateAllStationsLoadMessage(int step)
        {
            UpdateAllStationsLoadMessage(step, "");
        }

        /// <summary>
        /// Show loading status on Checklist page of All Stations data. 
        /// </summary>
        /// <param name="step">If 0, clear & hide message box</param>
        /// <param name="extra">if present for step 3, should be of form "3 of 14" or "45 of 250 (max)"</param>
        private void UpdateAllStationsLoadMessage(int step, string extra)
        {
            App.MyAssert(step >= 0 && step < 6);
            // Step 3 has 2 variant messages. We handle the plain variant here; the caller handles the bells & whistles cases
            // Broken out as separate function July 2015 release 5 version 3.4
            string updateMsg = "";
            if(step !=0)
            {
                //was, but shorten further: updateMsg = "Updating reports ";
                updateMsg = "Get reports ";
                if (App.CurrentVisualState != "vs321To500Wide" && App.CurrentVisualState != "vs320Wide") // Added for Release 3, revised further for 5
                    updateMsg += "from all stations for this event ";

                updateMsg += "[Step " + step.ToString() + " of 5";
                if (step == 3 && !String.IsNullOrEmpty(extra))
                    updateMsg += "; report " + extra;
                updateMsg += "]";
            }

            if (TP8.BasicPageChecklist.staticGettingAllStationsReports != null)
            {
                TP8.BasicPageChecklist.staticGettingAllStationsReports.Text = updateMsg;
                if (step == 0)
                    TP8.BasicPageChecklist.staticGettingAllStationsReports.Visibility = Visibility.Collapsed; // cleanup
            }

            if(TP8.SplitPage.staticSortedByOrLoadingText != null)
            {
                App.MyAssert(App.CurrentSearchResultsGroupName == "Outbox" || App.CurrentSearchResultsGroupName == "AllStations");
                if(App.CurrentSearchResultsGroupName == "Outbox")
                    return;

                updateMsg = updateMsg.Replace("from all stations ", ""); // not necessary to say this context of All Stations page. Good to shorten
                TP8.SplitPage.staticSortedByOrLoadingText.Text = updateMsg;
                if (step == 0)
                    UpdateSortedBySubtitle(); // cleanup
            }
        }

        public void UpdateSortedBySubtitle() // See also similar function in SplitPage.cs. This is callable throughout because of static pointer to field
        {
            // June 2015, sortedByTextPortrait dropped, only need sortedByText
            if (App.CurrentVisualState == "FullScreenLandscape" || App.CurrentVisualState == "FullScreenPortrait" ||
                App.CurrentVisualState == "Over1365Wide" || App.CurrentVisualState == "vs1026To1365Wide")
                TP8.SplitPage.staticSortedByOrLoadingText.Text = "Sorted " + App.PatientDataGroups.GetShortSortDescription(); // Similar to DefaultViewModel["QueryText"] in SearchResultsPage
            else
            {
                string s = App.PatientDataGroups.GetVeryShortSortDescription();
                //string s = App.PatientDataGroups.GetShortSortDescription();
                //s = s.Replace(", ascending", ""); // just leave uparrow or downarrow to indicate sort method
                //s = s.Replace(", descending", "");
                //s = s.Replace("triage ", ""); // just say "zone (alphabetic)"
                TP8.SplitPage.staticSortedByOrLoadingText.Text = s;
            }
        }

        private async Task ProcessAllStationsListImpl(string plname, string plpass, bool onStartup, bool invalidateCacheFirst) 
        {
            // now done in UpdateAllStationsLoadMessage:
            //   string updateMsg = "Updating reports from all stations for this event ";
            //   if (App.CurrentVisualState == "vs321To500Wide" || App.CurrentVisualState == "vs320Wide") // Added for Release 3
            //      updateMsg = "Updating reports ";

            // Compare Win 7 FormTriagePic.ProcessEventList(...)
            MessageDialog ImmediateDlg = new MessageDialog("");
            if (invalidateCacheFirst) // do this only if current event has changed
            {
                UpdateAllStationsLoadMessage(1);  // Purging cache;
                await PurgeCachedAllStationsList();
            }

            UpdateAllStationsLoadMessage(2); // Calling web service;

            List<Search_Response_Toplevel_Row> responseRows = null; // likewise
            string s;
            s = await App.service.GetReportsFromAllStationsCurrentEvent(); // was, but now uses global token: (plname, plpass);
            if (s.StartsWith("ERROR:"))
            {
                // For the user, this is not an error
                if(onStartup)
                    App.DelayedMessageToUserOnStartup = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - List of reports from all stations, for current event\n";
                else
                {
                    ImmediateDlg.Content = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - List of reports from all stations, for current event\n";
                    await ImmediateDlg.ShowAsync();
                }
                return;
            }

            if(s == "")
            {
                string msg = "For this event, there are no reports yet, from any stations or organizations reporting to our TriageTrak.\n" +
                    "Either the event is new or any earlier reports were purged.";
                if (onStartup)
                    App.DelayedMessageToUserOnStartup = msg;
                else
                {
                    ImmediateDlg.Content = msg;
                    await ImmediateDlg.ShowAsync();
                }
                return;
            }

            UpdateAllStationsLoadMessage(3); // Reading web service results into memory;
            responseRows = JsonConvert.DeserializeObject<List<Search_Response_Toplevel_Row>>(s);
            // Hopefully don't have to add more filtering here... we'll see.
            if (responseRows == null) // Assume this is an error, not just zero reports
            {
                if (onStartup)
                    // For the user, this is not an error
                    App.DelayedMessageToUserOnStartup = App.NO_OR_BAD_WEB_SERVICE_PREFIX +
                        "  - Valid list of reports from all stations, for the current event\n"; // Let's add 'Valid' here, for programmer to distinguish from prev msg.
                else
                {
                    ImmediateDlg.Content = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - Valid ist of reports from all stations, for the current event\n";
                    await ImmediateDlg.ShowAsync();
                }
                return;
            }

            _allstations.Clear(); // throw away stale data
            int i = 0;
            App.MyAssert(responseRows.Count <= 250);// Limit set in LPF_SOAP
            string count = (responseRows.Count).ToString();
            if (responseRows.Count == 250) 
                count = "250 (max)";
            foreach (var item in responseRows)
            {
                i++;
                //if (TP8.BasicPageChecklist.staticGettingAllStationsReports != null)
                //    TP8.BasicPageChecklist.staticGettingAllStationsReports.Text = updateMsg + "[Step 3 of 5; report " +
                //        i.ToString() + " of " + count + "]";// "Reading web service results into memory";
                string x = i.ToString() + " of " + count; // Function will make it read "...[Step 3 of 5; report 14 of 45]"
                UpdateAllStationsLoadMessage(3, x);// "Reading web service results into memory";

                // TO DO:  In future version, support Practice.  For now, skip any records so labeled
                if (item.edxl != null && item.edxl.Count() > 0 && item.edxl[0].mass_casualty_id.StartsWith("Practice-"))
                    continue;

                // No:_allstations.Add(await LoadNewPatientReportTextAndImage(item, new TP_PatientReport()));
                //No, causes loop artifacts: SampleDataSource.RefreshOutboxAndAllStationsItems();
            //}

                TP_PatientReport pr = new TP_PatientReport();
                pr.LoadFromJsonSearchResponse(item);
                if (pr.ImageEncoded.Contains("plus_cache")) // sanity check
                    await LoadNewPatientReportImage(pr);
                _allstations.Add(pr);

            }
            // separate loop to load photos:
#if MAYBENOT
            // We're getting "Collection was modified; eumerationoperation may not execute" problem.
            // So add ".ToList()" to make copy.
            List<TP_PatientReport> all = new List<TP_PatientReport>();
            all = _allstations.ToList();
            //foreach (TP_PatientReport i in _allstations)
            //{
            //        all.Add(i);
            //}
            _allstations.Clear(); // avoid memory clog

            foreach (var p in all)
            {
                if (p.ImageEncoded.Contains("plus_cache")) // sanity check
                    await LoadNewPatientReportImage(p);
            }
            _allstations.ReplaceWithList(all);
#endif
#if STILLERROR
            foreach (var p in _allstations.GetAsList())
            {
                if (p.ImageEncoded.Contains("plus_cache")) // sanity check
                    await LoadNewPatientReportImage(p);
            }
#endif
            UpdateAllStationsLoadMessage(4); // Also caching results to file;
            await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);
            // Caller will take care of _allstationssorted, _allstationssortedandfiltered
        }

        public async Task ProcessOutboxList(string plname, string plpass)
        {
            await ProcessOutboxList(plname, plpass, false); // = on startup
        }

        public async Task ProcessOutboxList(string plname, string plpass, bool onStartup) //, bool clearFirst)
        {
            App.MyAssert(_outbox.Count() == 0);
            // When we got here, we've ALREADY tried reading cache file, so don't try again.
            List<Search_Response_Toplevel_Row> responseRows = null; // likewise
            string s;
            s = await App.service.GetReportsForOutboxAllOpenEvents(false /* = all orgs*/);  // Get all orgs now, so that user later can toggle client-side filtering with or without current org
            // was before Feb 2015: GetReportsForOutboxCurrentEvent()
            // Called earlier GetReportsForOutbox(); // was, but now uses global token: (plname, plpass);
            if (s.StartsWith("ERROR:"))
            {
                // For the user, this is not an error
                MessageDialog ImmediateDlg = new MessageDialog("");
                if (onStartup)
                    App.DelayedMessageToUserOnStartup = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - Outbox list\n";
                else
                {
                    ImmediateDlg.Content = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - Outbox list\n";
                    await ImmediateDlg.ShowAsync();
                }
                App.DelayedMessageToUserOnStartup = App.NO_OR_BAD_WEB_SERVICE_PREFIX + "  - Outbox list\n";
                //MessageDialog dlg = new MessageDialog("Could not connect to web service to get disaster event list.  Using local cached version instead.");
                //await dlg.ShowAsync();
            }
            else
            {
                //BitmapImage bmi = null;
                responseRows = JsonConvert.DeserializeObject<List<Search_Response_Toplevel_Row>>(s);
                // Hopefully don't have to add more filtering here... we'll see.
                if (responseRows != null)
                {
                    foreach (var item in responseRows)
                    {
                        if (item.reporter_username != plname)
                            continue; // someone other than me sent it.  Note that Outbox in TP8 is per-user, not per-device
                        if (item.edxl == null || item.edxl.Count() == 0)
                            continue; // not originated by mobile device
                        // Depending on Win8 OS user setting, the login_account may not be available, either currently or as reported earlier within retrieved records.
                        // So screening here must be less rigorous (as of Feb 2015):
                        if (!String.IsNullOrEmpty(item.edxl[0].login_account) && !String.IsNullOrEmpty(App.UserWin8Account) && item.edxl[0].login_account != App.UserWin8Account)
                            continue;
                        if (item.edxl[0].login_machine != App.DeviceName)
                            continue;
                        // TO DO:  In future version, support Practice.  For now, skip any records so labeled
                        if (item.edxl[0].mass_casualty_id.StartsWith("Practice-"))
                            continue;
                        _outbox.Add(await LoadNewPatientReportTextAndImage(item, new TP_PatientReport()));
                        // No, causes loop artifacts: SampleDataSource.RefreshOutboxItems();
                    }
                }
                // Note that, unlike with AllStations, we are not enforcing a 250 record limit here, though it could exceed that because it concatenates reports from events.
                // To do that right, we would have get a report count associated with every event first, prioritize them if the total was over 250, e.g., always include
                // current event, and/or sort by event start date, discarding oldest.
                // Of course, if we had more sophisticated memory management, maybe we wouldn't need any record limit.
                await _outbox.WriteXML();
            }
        }

        private async Task<TP_PatientReport> LoadNewPatientReportTextAndImage(Search_Response_Toplevel_Row item, TP_PatientReport pr)
        {
            // Similar to event/incident list handling:
            pr.LoadFromJsonSearchResponse(item);
            // TO DO: make mapping in TP_PatientReport smarter, retain & use sha1 hashes
            if (item.images != null && item.images.Count() > 0)
            {
                string path = pr.ImageEncoded;
                App.MyAssert(path.Contains("plus_cache")); // sanity check
                //bmi = new BitmapImage(new Uri(path));

                pr.ImageWriteableBitmap = await pr.LoadImageWriteableBitmapFromWeb(new Uri(path));
                if (pr.ImageWriteableBitmap == null)
                {
                    pr.ImageEncoded = null; // new Dec 2014
                    await App.ErrorLog.ReportToErrorLog("From TP_PatientReportsSource.LoadNewPatientReportTextAndImage",
                        "Bad image uri: " + path + "\nPatient ID:" + pr.PatientID, false);
                }
                else
                    pr.ImageEncoded = await pr.GetImageAsBase64Encoded(); // overwrite ImageEncoded
            }
            return pr;
        }

        private async Task<TP_PatientReport> LoadNewPatientReportImage(TP_PatientReport pr)
        {
            // TO DO: make mapping in TP_PatientReport smarter, retain & use sha1 hashes
            string path = pr.ImageEncoded;
            App.MyAssert(path.Contains("plus_cache")); // sanity check
            //bmi = new BitmapImage(new Uri(path));

            pr.ImageWriteableBitmap = await pr.LoadImageWriteableBitmapFromWeb(new Uri(path));
            if (pr.ImageWriteableBitmap == null)
            {
                pr.ImageEncoded = null; // new Dec 201
                await App.ErrorLog.ReportToErrorLog("From TP_PatientReportsSource.LoadNewPatientReportImage",
                    "Bad image uri: " + path + "\nPatient ID:" + pr.PatientID, false);
            }
            else
                pr.ImageEncoded = await pr.GetImageAsBase64Encoded(); // overwrite ImageEncoded
            return pr;
        }

        private async Task Scrub() //was: private void Scrub(), without awaits
        {
            await ScrubOutbox();
            await ScrubAllStations();
        }

        public async Task ScrubOutbox()//was: public void ScrubOutbox(), without await Task.Run...
        {
            await Task.Run( () => ScrubImpl(_outbox));
        }

        public async Task ScrubAllStations() //was: public void ScrubAllStations(), without await Task.Run...
        {
            await Task.Run( () => ScrubImpl(_allstations));
        }

        private void ScrubImpl(TP_PatientReports l)
        {
            // Remove any records without patient ID
            List<TP_PatientReport> scrubL = new List<TP_PatientReport>();
            foreach (TP_PatientReport i in l)
            {
                if (!String.IsNullOrEmpty(i.PatientID))
                    scrubL.Add(i);
            }
            l.ReplaceWithList(scrubL);
        }

        public async Task ReSortAndFilter() // was: public void ReSortAndFilter(). Change & Task.Run's added for v 3.5
        {
            await ReSortAndFilterImplAsync(_outbox, _outboxsorted, _outboxsortedandfiltered, true, true);
            OutboxForStatisticsRefresh(); // new June 2015
            await ReSortAndFilterImplAsync(_allstations, _allstationssorted, _allstationssortedandfiltered, true, false);
        }

        public async Task ReSortAndMinimallyFilter() // used by SplitPage, filter is only "current event only" checkbox
        {
            await ReSortAndMinimallyFilterOutbox();
            await ReSortAndFilterImplAsync(_allstations, _allstationssorted, _allstationssortedandfiltered, false, false);
        }

        public async Task ReSortAndMinimallyFilterOutbox() // used by SplitPage, filter is only "current event only" checkbox
        {
            // Broken out as separate function version 3.6 August 2015
            // was: public void ReSortAndFilter(). Change & Task.Run's added for v 3.5
            await ReSortAndFilterImplAsync(_outbox, _outboxsorted, _outboxsortedandfiltered, false, true);
            OutboxForStatisticsRefresh(); // new June 2015
        }

        /// <summary>
        /// Create background task for sort & filtering
        /// </summary>
        /// <param name="origL"></param>
        /// <param name="sortL"></param>
        /// <param name="sortfiltL"></param>
        /// <param name="useFullFilter"></param>
        /// <param name="isOutbox"></param>
        /// <returns></returns>
        public async Task ReSortAndFilterImplAsync(TP_PatientReports origL, TP_PatientReports sortL, TP_PatientReports sortfiltL, bool useFullFilter, bool isOutbox)
        {
            await Task.Run(() => ReSortAndFilterImpl(origL, sortL, sortfiltL, useFullFilter, isOutbox));
        }

        /// <summary>
        /// When called, converts TP_PatientReports to List<TP_PatientReport>
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="sort"></param>
        /// <param name="sortfilt"></param>
        private void ReSortAndFilterImpl(TP_PatientReports origL, TP_PatientReports sortL, TP_PatientReports sortfiltL, bool useFullFilter, bool isOutbox)
        {
            // If I could figure out how to define TP_PatientReports.OrderBy, wouldn't need these conversions here and at end:
            List<TP_PatientReport> orig = origL.GetAsList();
            List<TP_PatientReport> sort = new List<TP_PatientReport>(); // NOT NEEDED: = sortL.GetAsList();
            List<TP_PatientReport> sortfilt = new List<TP_PatientReport>(); // NOT NEEDED: = sortfiltL.GetAsList();

            // NOT NEEDED: sort.Clear();
            if (App.SortFlyoutAscending)
            {
                switch (App.SortFlyoutItem)
                {
                    case App.SortByItem.Gender:
                        sort = orig.OrderBy(o => o.Gender).ToList(); break;
                    case App.SortByItem.FirstName:
                        sort = orig.OrderBy(o => o.FirstName).ToList(); break;
                    case App.SortByItem.LastName:
                        sort = orig.OrderBy(o => o.LastName).ToList(); break;
                    case App.SortByItem.AgeGroup:
                        sort = orig.OrderBy(o => o.AgeGroup).ToList(); break;
                    case App.SortByItem.PatientIdSkipPrefixNumeric:
                        sort = orig.OrderBy(o => o.PatientID, new PatientIdComparer()).ToList(); break;
                    case App.SortByItem.PatientIdWithPrefixAlphabetic:
                        sort = orig.OrderBy(o => o.PatientID).ToList(); break;
                    case App.SortByItem.TriageZone:
                        sort = orig.OrderBy(o => o.Zone).ToList(); break;
                    case App.SortByItem.ArrivalTime:
                        sort = orig.OrderBy(o => o.WhenLocalTimeMsg1).ToList(); break; // Or maybe sent.  probably needs more work, use of timezone or UTC? Before Release 6, was (WhenLocalTime)
                    case App.SortByItem.DisasterEvent:
                        sort = orig.OrderBy(o => o.EventName).ToList(); break; // or maybe Event ID
                    default:
                        sort = orig.OrderByDescending(o => o.FirstName).ToList(); break;
                }
#if NEEDSWORK
                    case App.SortByItem.PLStatus:
                        sort = orig.OrderBy(o => o.PLStatus).ToList(); break;
                    case App.SortByItem.ReportingStation:
                        sort = orig.OrderBy(o => o.).ToList(); break;
                    maybe also PicCount, whether there is comment

#endif
            }
            else
            {
                switch (App.SortFlyoutItem)
                {
                    case App.SortByItem.Gender:
                        sort = orig.OrderByDescending(o => o.Gender).ToList(); break;
                    case App.SortByItem.FirstName:
                        sort = orig.OrderByDescending(o => o.FirstName).ToList(); break;
                    case App.SortByItem.LastName:
                        sort = orig.OrderByDescending(o => o.LastName).ToList(); break;
                    case App.SortByItem.AgeGroup:
                        sort = orig.OrderByDescending(o => o.AgeGroup).ToList(); break;
                    case App.SortByItem.PatientIdSkipPrefixNumeric:
                        sort = orig.OrderByDescending(o => o.PatientID, new PatientIdComparer()).ToList(); break;
                    case App.SortByItem.PatientIdWithPrefixAlphabetic:
                        sort = orig.OrderByDescending(o => o.PatientID).ToList(); break;
                    case App.SortByItem.TriageZone:
                        sort = orig.OrderByDescending(o => o.Zone).ToList(); break;
                    case App.SortByItem.ArrivalTime:
                        sort = orig.OrderByDescending(o => o.WhenLocalTimeMsg1).ToList(); break; // Or maybe sent.  probably needs more work, use of timezone or UTC? Before Release 6, was o.WhenLocalTime
                    case App.SortByItem.DisasterEvent:
                        sort = orig.OrderByDescending(o => o.EventName).ToList(); break; // or maybe Event ID
                    default:
                        sort = orig.OrderByDescending(o => o.FirstName).ToList(); break;
                }
#if NEEDSWORK
                    case App.SortByItem.PLStatus:
                        sort = orig.OrderByDescending(o => o.PLStatus).ToList(); break;
                    case App.SortByItem.ReportingStation:
                        sort = orig.OrderByDescending(o => o.).ToList(); break;
                    maybe also PicCount, whether there is comment

#endif
            }

            if (useFullFilter)
            {
                // NOT NEEDED: sortfilt.Clear();
                foreach (TP_PatientReport i in sort)
                {
                    if (InFilteredResults(i, App.CurrentSearchQuery))
                        sortfilt.Add(i);
                }
            }
            else
            {
                // Use limited filtering of Outbox, All Stations splitview.
                // For fastest speed, do some of the logic outside of loops
                if (isOutbox)
                {
                    if (!App.OutboxCheckBoxCurrentEventOnly && !App.OutboxCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                            sortfilt.Add(i);
                    }
                    else if (App.OutboxCheckBoxCurrentEventOnly && App.OutboxCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.EventName == App.CurrentDisaster.EventName && i.OrgName == App.CurrentOrgContactInfo.OrgName)
                                sortfilt.Add(i);
                        }
                    }
                    else if (App.OutboxCheckBoxCurrentEventOnly && !App.OutboxCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.EventName == App.CurrentDisaster.EventName)
                                sortfilt.Add(i);
                        }
                    }
                    else if (!App.OutboxCheckBoxCurrentEventOnly && App.OutboxCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.OrgName == App.CurrentOrgContactInfo.OrgName)
                                sortfilt.Add(i);
                        }
                    }
                }
                else // All Stations split view doesn't implement current event checkbox.  It is always set to true.
                {

                    if (!App.AllStationsCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                            sortfilt.Add(i);
                    }
                    else
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.OrgName == App.CurrentOrgContactInfo.OrgName)
                                sortfilt.Add(i);
                        }
                    }
                    /* Alternative would be to sanity-check this, but we're ONLY loading current event
                    if (App.AllStationsCheckBoxMyOrgOnly)
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.EventName == App.CurrentDisaster.EventName && i.OrgName == App.CurrentOrgContactInfo.OrgName)
                                sortfilt.Add(i);
                        }
                    }
                    else
                    {
                        foreach (TP_PatientReport i in sort)
                        {
                            if (i.EventName == App.CurrentDisaster.EventName)
                                sortfilt.Add(i);
                        }
                    } */

                }
            }

            // DON'T NEED, because orig not altered: origL.ReplaceWithList(orig);
/* DIDN'T HELP
            sortL.Clear(); // might help stop flashing
            sortfiltL.Clear(); // ditto
            SampleDataSource.RefreshOutboxAndAllStationsItems(); // Propagate here.  ditto */
            sortL.ReplaceWithList(sort);
            sortfiltL.ReplaceWithList(sortfilt);

        }

        /// <summary>
        /// When called, rebuilds outboxforstatistics from outbox
        /// </summary>
        public void OutboxForStatisticsRefresh()
        {
            // If I could figure out how to define TP_PatientReports.OrderBy, wouldn't need these conversions here and at end:
            List<TP_PatientReport> orig = _outbox.GetAsList();
            List<TP_PatientReport> clone = new List<TP_PatientReport>(); // NOT NEEDED: = sortL.GetAsList();
/* WAS prior to Release 6:
            foreach (TP_PatientReport i in orig)
            {
                if(String.IsNullOrEmpty(i.WhenLocalTime))
                    continue; // skip messed-up records
                TP_PatientReport r = new TP_PatientReport(i); // make deep copy
                r.WhenLocalTime = ParseDate(r.WhenLocalTime); // perform surgery on clone, leave orig unaffected.
                clone.Add(r);
            }
            // sort by local time, oldest first, as required by algorithms in Statistics:
            clone = clone.OrderBy(o => o.WhenLocalTime).ToList(); */
            foreach (TP_PatientReport i in orig)
            {
                if (String.IsNullOrEmpty(i.WhenLocalTime))
                    continue; // skip messed-up records
                TP_PatientReport r = new TP_PatientReport(i); // make deep copy
                r.WhenLocalTime = ParseDate(r.WhenLocalTime); // perform surgery on clone, leave orig unaffected.
                if (!String.IsNullOrEmpty(i.WhenLocalTimeMsg1))
                    r.WhenLocalTimeMsg1 = ParseDate(r.WhenLocalTimeMsg1); // ditto
                else
                    r.WhenLocalTimeMsg1 = r.WhenLocalTime; // fixup, shouldn't ordinarily be needed
                clone.Add(r);
            }
            // sort by local time of arrival, oldest first, as required by algorithms in Statistics:
            clone = clone.OrderBy(o => o.WhenLocalTimeMsg1).ToList();

            _outboxforstatistics.ReplaceWithList(clone);
        }

        /// <summary>
        /// Take first 10 characters, for date only, ignore time and UTC offset
        /// </summary>
        /// <param name="WhenLocalTime"></param>
        /// <returns></returns>
        private string ParseDate(string WhenLocalTime_)
        {
            return WhenLocalTime_.Substring(0, 10);// Assume YYYY-MM-DD format.
        }

        public class PatientIdComparer : IComparer<string>
        {
            // Adapted from Jeff Paulsen, stackoverflow.com/questions/6396378/c-sharp-linq-orderby-numbers-that-are-string-and-you-cannot-convert-them-to-int
            public int Compare(string s1, string s2)
            {
                int i1 = RemovePrefix(s1);
                int i2 = RemovePrefix(s2);
                if (i1 > i2) return 1;
                if (i1 < i2) return -1;
                return 0; // equal
            }

            public static int RemovePrefix(string s)
            {
                if (String.IsNullOrEmpty(s) || !Char.IsDigit(s, s.Length-1))
                    return 0; // something wrong
                if (s.Length == 1 && Char.IsDigit(s[0]))
                    return Convert.ToInt32(s); // special case

                int i;
                for (i = s.Length - 1; i > 0; i--)
                {
                    if (!Char.IsDigit(s, i))
                    {
                        i++; // point to leftmost digit after prefix
                        break;
                    }
                }
                App.MyAssert(i >= 0);
                s = s.Substring(i);
                return Convert.ToInt32(s);
            }
#if ORIG
            public int Compare(string s1, string s2)
            {
                if (IsNumeric(s1) && IsNumeric(s2))
                {
                    int i1 = Convert.ToInt32(s1);
                    int i2 = Convert.ToInt32(s2);
                    if (i1 > i2) return 1;
                    if (i1 < i2) return -1;
                    return 0; // equal
                }
                if (IsNumeric(s1) && !IsNumeric(s2))
                    return -1;
                if (!IsNumeric(s1) && !IsNumeric(s2))
                    return 1;

                return String.Compare(s1, s2);

            }

            public static bool IsNumeric(string s)
            {
                try
                {
                    int i = Convert.ToInt32(s);
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }
#endif
        }

        public string GetShortSortDescription()
        {
            string desc = " by ";

            switch (App.SortFlyoutItem)
            {
                case App.SortByItem.Gender:
                    desc += "gender"; break;
                case App.SortByItem.FirstName:
                    desc += "first name"; break;
                case App.SortByItem.LastName:
                    desc += "last name"; break;
                case App.SortByItem.AgeGroup:
                    desc += "age group"; break;
                case App.SortByItem.PatientIdSkipPrefixNumeric:
                    desc += "casualty ID (number only)"; break; // was: ...(numeric)
                case App.SortByItem.PatientIdWithPrefixAlphabetic:
                    desc += "casualty ID (full)"; break; // was: ...(alphabetic
                case App.SortByItem.TriageZone:
                    desc += "triage zone (alphabetic)"; break;
                case App.SortByItem.ArrivalTime:
                    desc += "arrival time & date"; break; // Or maybe sent.  probably needs more work, use of timezone or UTC?
                case App.SortByItem.DisasterEvent:
                    desc += "disaster event"; break; // or maybe Event ID
                default:
                    desc += "unknown sort method"; break;
            }
            if (App.SortFlyoutAscending)
                desc += ", ascending \u25B2"; // Unicode "Black up-pointing triangle".  Present in Segoe UI Symbol Font
            else
                desc += ", descending \u25BC";
            return desc;
        }

        public string GetVeryShortSortDescription()
        {
            string desc = " by ";

            switch (App.SortFlyoutItem)
            {
                // 9 letters or less here
                case App.SortByItem.Gender:
                    desc += "gender"; break;
                case App.SortByItem.FirstName:
                    desc += "1st name"; break;
                case App.SortByItem.LastName:
                    desc += "last name"; break;
                case App.SortByItem.AgeGroup:
                    desc += "age group"; break;
                case App.SortByItem.PatientIdSkipPrefixNumeric:
                    desc += "ID (number)"; break;
                case App.SortByItem.PatientIdWithPrefixAlphabetic:
                    desc += "ID (full)"; break;
                case App.SortByItem.TriageZone:
                    desc += "zone name"; break;
                case App.SortByItem.ArrivalTime:
                    desc += "arrival"; break; // Or maybe sent.  probably needs more work, use of timezone or UTC?
                case App.SortByItem.DisasterEvent:
                    desc += "event"; break; // or maybe Event ID
                default:
                    desc += "???"; break;
            }
            if (App.SortFlyoutAscending)
                desc += " \u25B2"; // Unicode "Black up-pointing triangle".  Present in Segoe UI Symbol Font
            else
                desc += " \u25BC"; // Unicode "Black down-pointing triangle".  Present in Segoe UI Symbol Font
            return desc;
        }

#if DONT_NEED
        /// <summary>
        /// Dummy data for _outbox and _allstations
        /// </summary>
        /// <param name="ltp"></param>
        private void AddItemsSet1(TP_PatientReports ltp)
        {
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:01:26 -04:00",
                "EDT",
                "2012-08-13T22:01:26Z", // Add the 4 hour difference to adjust to UTC.  Yeah, we could right a routine to read 1st item & gen this.
                "911-0001",
                "Yellow",
                "Male",
                "Adult",
                "John",
                "Doe",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:02:26 -04:00",
                "EDT",
                "2012-08-13T22:01:26Z",
                "911-0002",
                "Red",
                "Female",
                "Adult",
                "Jane",
                "Doe",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:03:26 -04:00",
                "EDT",
                "2012-08-13T22:03:26Z",
                "911-0003",
                "Yellow",
                "Female",
                "Youth",
                "Janet",
                "Doe",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:04:26 -04:00",
                "EDT",
                "2012-08-13T22:04:26Z",
                "911-0004",
                "BH Green",
                "Female",
                "Adult",
                "Karen",
                "Deer",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:05:26 -04:00",
                "EDT",
                "2012-08-13T22:05:26Z",
                "911-0005",
                "Red",
                "Male",
                "Adult",
                "Alfred",
                "Alcott",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:06:26 -04:00",
                "EDT",
                "2012-08-13T22:06:26Z",
                "911-0006",
                "Green",
                "Male",
                "Youth",
                "Basil",
                "McDowell",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:07:26 -04:00",
                "EDT",
                "2012-08-13T22:07:26Z",
                "911-0007",
                "Black",
                "Male",
                "Adult",
                "", // [No name]
                "",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "first ambulance"
                ));
        }
            // Use the timestamp as UniqueID.  In Win7 TriagePic, this would be either:
            // EDXL format ("dateEDXL"), e.g., 2012-08-13T22:24:26Z
            // WhenLocalTime, e.g., 2012-08-13 18:24:26 -04:00  (with time zone "TZ" also given, e.g., EDT)
            // Latter is more informative

        /// <summary>
        /// Dummy data for _outbox and _allstations
        /// </summary>
        /// <param name="ltp"></param>
        private void AddItemsSet2(TP_PatientReports ltp)
        {

            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:08:26 -04:00",
                "EDT",
                "2012-08-13T22:08:26Z",
                "911-0008",
                "Yellow",
                "Male",
                "Adult",
                "Thomas \"Tommy\"",
                "Belamy",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:09:26 -04:00",
                "EDT",
                "2012-08-13T22:09:26Z",
                "911-0009",
                "Green",
                "Female",
                "Adult",
                "Mary Jane",
                "Bolt",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:10:26 -04:00",
                "EDT",
                "2012-08-13T22:10:26Z",
                "911-0010",
                "Yellow",
                "Female",
                "Youth",
                "Bella",
                "Zuber",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:11:26 -04:00",
                "EDT",
                "2012-08-13T22:11:26Z",
                "911-0011",
                "Gray",
                "Female",
                "Adult",
                "", //[No Name]
                "",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:12:26 -04:00",
                "EDT",
                "2012-08-13T22:22:26Z",
                "911-0012",
                "Red",
                "Male",
                "Adult",
                "Maximillian",
                "Glum",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:13:26 -04:00",
                "EDT",
                "2012-08-13T22:13:26Z",
                "911-0008",
                "Green",
                "Male",
                "Youth",
                "Toc",
                "Flint",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));
            ltp.Add(new TP_PatientReport(
                "2012-08-13 18:14:26 -04:00",
                "EDT",
                "2012-08-13T22:14:26Z",
                "911-0014",
                "Black",
                "Male",
                "Adult",
                "Michael",
                "Redcliff",
                "rockville",
                "Rockville earthquake",
                "TEST/DEMO/DRILL",
                "second ambulance"
                ));

        }
#endif
        /// <summary>
        /// Applies flyout filters to candidate items for listing
        /// </summary>
        /// <param name="i"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        private bool InFilteredResults(TP_PatientReport i, string query) // compare InSearchResults(SampleDataItem i, string query);
        {
            if (String.IsNullOrEmpty(i.PatientID))
                return false; // Actually an ongoing problem to be solved

            string patientName = (i.FirstName + " " + i.LastName).ToLower();
            if (patientName == " ")
                patientName = "";
            query = query.ToLower(); // Added Release 7. Why wasn't this picked up earlier? Neglect, or did something change in how query come here, or how  .Contains is processed below?
            string patientID = i.PatientID.ToLower();
            if (String.IsNullOrEmpty(patientName) && !App.CurrentFilterProfile.IncludeHasNoPatientName)
                return false;
            if (!String.IsNullOrEmpty(patientName) && !App.CurrentFilterProfile.IncludeHasPatientName)
                return false;
            if (App.CurrentFilterProfile.ReportedAtMyOrgOnly && (i.OrgName != App.CurrentOrgContactInfo.OrgName))
                return false;
            App.MyAssert(!String.IsNullOrEmpty(i.Gender));
            switch (i.Gender)
            {
                case "Male":
                    if (!App.CurrentFilterProfile.IncludeMale)
                        return false; break;
                case "Female":
                    if (!App.CurrentFilterProfile.IncludeFemale)
                        return false; break;
                case "Unknown": case "Complex Gender":
                    if (!App.CurrentFilterProfile.IncludeUnknownOrComplexGender)
                        return false; break;
                default:
                    App.MyAssert(false);
                    return false;
            }
            App.MyAssert(!String.IsNullOrEmpty(i.AgeGroup));
            switch (i.AgeGroup)
            {
                case "Adult":
                    if (!App.CurrentFilterProfile.IncludeAdult)
                        return false; break;
                case "Youth":
                    if (!App.CurrentFilterProfile.IncludePeds)
                        return false; break;
                case "Unknown Age Group": case "Other Age Group (e.g., Expectant)":
                    if (!App.CurrentFilterProfile.IncludeUnknownAgeGroup)
                        return false; break;
                default:
                    App.MyAssert(false);
                    return false;
            }
            App.MyAssert(!String.IsNullOrEmpty(i.Zone));
            if(!App.CurrentFilterProfile.IncludeWhichZones.IsIncluded(i.Zone)) // if i.Zone not found, treated as false too.
                return false;
/* WAS:
            switch (i.Zone)
            {

                case "Green":
                    if (!App.CurrentFilterProfile.IncludeGreenZone)
                        return false; break;
                case "BH Green":
                    if (!App.CurrentFilterProfile.IncludeBHGreenZone)
                        return false; break;
                case "Yellow":
                    if (!App.CurrentFilterProfile.IncludeYellowZone)
                        return false; break;
                case "Red":
                    if (!App.CurrentFilterProfile.IncludeRedZone)
                        return false; break;
                case "Gray":
                    if (!App.CurrentFilterProfile.IncludeGrayZone)
                        return false; break;
                case "Black":
                    if (!App.CurrentFilterProfile.IncludeBlackZone)
                        return false; break;
                default:
                    App.MyAssert(false);
                    return false; 
            }
*/
            //if (!String.IsNullOrEmpty(i.nPicCount)) // Until picCount is fully implemented, just skip if empty
            {
                if(i.nPicCount == 0 && !App.CurrentFilterProfile.IncludeHasNoPhoto)
                    return false;
                if(i.nPicCount != 0 && !App.CurrentFilterProfile.IncludeHasPhotos)
                    return false;
            }
            App.MyAssert(!String.IsNullOrEmpty(i.EventType));
            App.MyAssert(i.EventType == "REAL" || i.EventType == "TEST/DEMO/DRILL"); // could change in future
            if (i.EventType == "REAL" && !App.CurrentFilterProfile.DisasterEventIncludeReal)
                return false;
            if (i.EventType == "TEST/DEMO/DRILL" && !App.CurrentFilterProfile.DisasterEventIncludeTest)
                return false;
            if((App.CurrentDisasterList.IsPublic(i.EventName) && !App.CurrentFilterProfile.DisasterEventIncludePublic))
                return false;
            if((App.CurrentDisasterList.IsPrivate(i.EventName) && !App.CurrentFilterProfile.DisasterEventIncludePrivate)) // Private for now include Hospital Users, Admins, Researchers
                return false;
            // See SearchResultsPage.xaml.cs for eventAndOrgText.Text setting
            string t = App.SearchResultsEventTitleTextBasedOnCurrentFilterProfile;
            if (t == "No Events" || t == "")
                return false; // Pathological case
            // Other valid group names are: "All Events", "All Public Events","All Private Events", "All Test Events","All Real Events", "All Private Test Events",
            // "All Private Real Events", "All Private Test Events", "All Public Real Events", and "All Public Test Events".

            if (App.CurrentFilterProfile.FlyoutEventComboBoxChoice == "Current event (recommended)"  && i.EventName != App.CurrentDisaster.EventName)
                return false;

            // Specific event:
            if (!(t.Contains("All ") && t.Contains(" Events")) && i.EventName != t)
                return false;

            if (query == string.Empty)
                return true;
            if (App.CurrentFilterProfile.SearchAgainstName && (patientName.StartsWith(query) || patientName.Contains(" " + query)))
                return true;
            if (App.CurrentFilterProfile.SearchAgainstID && patientID.Contains(query))
                return true;
            return false;
#if TODO
            String whenLocalTime,
            //String dateEDXL,
            String timezone,
            String sent,
            String patientID, // includes prefix
            String zone,
            String gender,
            String ageGroup,
            String firstName,
            String lastName,
            String picCount,
            String eventShortName,
            String eventName, // w/o suffix
            String eventType, // suffix
            String imagePrefix,
            String imagePath,
            String comments

            App.CurrentFilterProfile.DisasterEventIncludePrivate;
            App.CurrentFilterProfile.DisasterEventIncludePublic;
            // Done elsewhere FlyoutEventComboBoxChoice = "All events";
            // Hard to support with current state of PL, choice of events based on "Hospital/Organization":
            //"Current (if known, otherwise Default)"
            //"All Sharing These Reports"
            //"Specified:" ComboBox

#endif
        }

        public async Task AddSendHistory(TP_PatientReport pr)
        {
            if (AddSendHistoryImpl(_outbox, pr))
            {
                await _outbox.WriteXML();
                // Only update send history of things sent from here... so if no update, then don't need to propagate to our all stations list.
                // Let's put in some isolation between the 2 lists, in terms of the object they reference:
                TP_PatientReport pr2 = new TP_PatientReport(pr);
                if (AddSendHistoryImpl(_allstations, pr2))
                    await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);

                // Assume at some point we will allow filtering/ordering on send code, so resort here
                await ReSortAndFilter(); //  Await added for v 3.5
                // must follow ReSortAndFilter:
                await SampleDataSource.RefreshOutboxItems(); // For benefit of next peek at Outbox. Await added for v 3.5
            }
        }

        private bool AddSendHistoryImpl(TP_PatientReports l, TP_PatientReport pr)
        {
            //int index = l.FindIndexByPatientID(pr.PatientID); // Needs more work!!!
            //if (index >= 0)
            //{
            //    App.MyAssert(false);
            //    return false; // no change
            //}

            l.Add(pr);
            return true; // change
        }
/* MAYBE NOT - REPLACE WITH UpdateSendHistory(pr)
        /// <summary>
        /// Call this if _outbox (and optionally _allstations) list in memory is already updated.
        /// </summary>
        /// <param name="allStationsToo"></param>
        /// <returns></returns>
        public async Task WriteSendHistory(bool allStationsToo)
        {
            await _outbox.WriteXML();
            // Only update send history of things sent from here... so if no update, then don't need to propagate to our all stations list.
            if (allStationsToo)
                await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);
            // Assume at some point we will allow filtering/ordering on send code, so resort here
            ReSortAndFilter();
            // must follow ReSortAndFilter:
            SampleDataSource.RefreshOutboxItems(); // For benefit of next peek at Outbox
        }
*/

        public async Task UpdateSendHistory(string patientID, SentCodeObj revisedObjSentCode)
        {
            // Assume that the msg version # was NOT revised, only the main code
            await UpdateSendHistory(patientID, revisedObjSentCode.GetVersionCount(), revisedObjSentCode.ToString(), false);
        }


        public async Task UpdateSendHistory(string patientID, int targetSentCodeVersion, string revisedSentCode)
        {
            await UpdateSendHistory(patientID, targetSentCodeVersion, revisedSentCode, false);
        }

        public async Task UpdateSendHistory(string patientID, int targetSentCodeVersion, string revisedSentCode, bool revisedSuperceded) // Compare Win7 outbox.UpdateSendHistory(...)
        {
            if (UpdateSendHistoryImpl(_outbox, patientID, targetSentCodeVersion, revisedSentCode, revisedSuperceded))
            {
                await UpdateSendHistoryAfterOutbox(patientID, targetSentCodeVersion, revisedSentCode, revisedSuperceded);
            }
        }

        private bool UpdateSendHistoryImpl(TP_PatientReports l, string patientID, int targetSentCodeVersion, string revisedSentCode, bool revisedSuperceded)
        {
            int index = l.FindIndexByPatientIDAndSentCodeVersion(patientID, targetSentCodeVersion);
            if (index < 0)
            {
                // No, all_stations may not have been set to the current event recently: App.MyAssert(false);
                return false; // no change
            }

            TP_PatientReport o = l.Fetch(index);
            // Maybe not copying sendcode right, could do:
            //TP_PatientReport o = new TP_PatientReport(l.Fetch(index));
            if (o.SentCode == revisedSentCode && o.Superceded == revisedSuperceded)
                return false; // no change

            o.SentCode = revisedSentCode;
            o.Superceded = revisedSuperceded;
            l.Update(o, index);
            return true; // change
        }

        /// <summary>
        /// Call this if _outbox list in memory is already updated, but not yet on disk
        /// Will save _outbox and propagate data to _allstations
        /// </summary>
        /// <param name="allStationsToo"></param>
        /// <returns></returns>
        public async Task UpdateSendHistoryAfterOutbox(string patientID, int targetSentCodeVersion, string revisedSentCode, bool revisedSuperceded)
        {
            await _outbox.WriteXML();
            // Only update send history of things sent from here... so if no update, then don't need to propagate to our all stations list.
            // Don't need to introduce pr2 here... object on _allstations list should already be separate from object on _outbox list
            if (UpdateSendHistoryImpl(_allstations, patientID, targetSentCodeVersion, revisedSentCode, revisedSuperceded))
                await _allstations.WriteXML(PATIENT_REPORTS_ALL_STATIONS_FILENAME);
            // Assume at some point we will allow filtering/ordering on send code, so resort here
            await ReSortAndFilter(); // Await added for v 3.5
            // must follow ReSortAndFilter:
            await SampleDataSource.RefreshOutboxItems(); // For benefit of next peek at Outbox. Await added for v 3.5
        }

        public async Task UpdateSendHistoryAfterOutbox(string patientID, SentCodeObj revisedObjSentCode)
        {
            // Assume that the msg version # was NOT revised, only the main code
            await UpdateSendHistoryAfterOutbox(patientID, revisedObjSentCode.GetVersionCount(), revisedObjSentCode.ToString(), false);
        }

    }
}
