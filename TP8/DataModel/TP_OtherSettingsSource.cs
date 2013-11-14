﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.UI.Popups;
//using TP8.Data;

namespace TP8.Data
{
    // Code here based on style of http jesseliberty.com/2012/08/21/windows-8-data-binding-part5binding-to-lists/
    // Note that choice of items here is quite a bit different than Win 7 TriagePic's OtherSettings.xml
    public class TP_OtherSettings : INotifyPropertyChanged
    {
        // skip for now: SchemaRevision

        private string _currentEventName;
        public string CurrentEventName // unlike Win 7, does not include " - " and type suffix
        {
            get { return _currentEventName; }
            set
            {
                _currentEventName = value;
                RaisePropertyChanged();
            }
        }

        private string _currentEventShortName;
        public string CurrentEventShortName
        {
            get { return _currentEventShortName; }
            set
            {
                _currentEventShortName = value;
                RaisePropertyChanged();
            }
        }

        // skip for now: event type
        // skip for now event range int

        private string _currentNewPatientNumber; // without prefix (unlike TP_PatientReport.PatientID)
        public string CurrentNewPatientNumber
        {
            get { return _currentNewPatientNumber; }
            set
            {
                _currentNewPatientNumber = value;
                RaisePropertyChanged();
            }
        }

        private string _currentNewPracticePatientNumber; // without prefix 
        public string CurrentNewPracticePatientNumber
        {
            get { return _currentNewPracticePatientNumber; }
            set
            {
                _currentNewPracticePatientNumber = value;
                RaisePropertyChanged();
            }
        }

        private string _plEndPointAddress;
        public string PLEndPointAddress
        {
            get { return _plEndPointAddress; }
            set
            {
                _plEndPointAddress = value;
                RaisePropertyChanged();
            }
        }
// Skip Win 7's: Hospital Name Selected (can be inferred elsewhere

        public event PropertyChangedEventHandler PropertyChanged;


        private void RaisePropertyChanged([CallerMemberName] string caller = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(caller));
            }
        }
/* FOR REF:
        public async Task<string> GetCurrentOrgContactInfo(string orgUuid, string orgName)
        {
            string status = await App.service.GetHospitalData(orgUuid, orgName); // Function call puts results directly into App.CurrentOrgContactInfo
            // could check status for "ERROR:" or "COMMUNICATIONS ERROR:"
            // If ERROR, there may or may not be anything put into App.CurrentOrgContactInfo
            return status;
        }
 */

    }

    // We're introducing a list ONLY so we can use LocalStorage class.  It's a kludge.

    [XmlType(TypeName = "OtherSettingsList")]
    public class TP_OtherSettingsList : IEnumerable<TP_OtherSettings>
    {
        const string OTHER_SETTINGS_FILENAME = "OtherSettings.xml";

        private List<TP_OtherSettings> inner = new List<TP_OtherSettings>();

        public void Add(object o)
        {
            inner.Add((TP_OtherSettings)o);
        }

        public void Remove(TP_OtherSettings o)
        {
            inner.Remove(o);
        }

        public void Clear()
        {
            inner.Clear();
        }

        public List<TP_OtherSettings> GetAsList()
        {
            return inner;
        }

        public void ReplaceWithList(List<TP_OtherSettings> list)
        {
            inner = list;
        }

        public IEnumerator<TP_OtherSettings> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void UpdateOrAdd(TP_OtherSettings o)
        {
            //int index = inner.FindIndex(i => i.EventShortName == o.EventShortName); // C# 3.0 lambda expression
            //if (index >= 0)
            //    inner[index] = o; // update
            if (inner.Count > 0)
            {
                App.MyAssert(inner.Count == 1);
                inner[0] = o;
            }
            else
                Add(o);
        }

        public async Task Init()
        {
            // This is called AFTER App.OrgDataList.Init()
            bool exists = await DoesFileExistAsync();
            //if (!exists)
            //    GenerateDefaultOrgContactInfo(); // This will provide a default XML file with a little content if none exists

            await ProcessOtherSettings(exists);

            // That is clear the list in memory, then call the web service.  If successful, memory list is available and also gets written to XML file.
            // If failed, will try to read the XML file instead.

        }

        private async Task<bool> DoesFileExistAsync()
        {
            return await LocalStorage.DoesFileExistAsync(OTHER_SETTINGS_FILENAME);
        }

        private async Task GenerateDefaultOtherSettings()
        {
            inner.Add(new TP_OtherSettings()
            {
                CurrentEventName = "Test with TriagePic",
                CurrentEventShortName = "testtp",
                CurrentNewPatientNumber = "0001",
                CurrentNewPracticePatientNumber = "0",
                PLEndPointAddress = "" // Empty means no over-ride
            });
            await WriteXML();
        }

        private async Task ProcessOtherSettings(bool exists)
        {
            // Doesn't yet use web service or file interrogation; nor practice number
            App.MyAssert(App.CurrentDisasterList.Count() != 0);

            if (App.CurrentOtherSettingsList.Count() != 0)
                return; // anticipating fresh install wizard

            App.MyAssert(App.CurrentDisaster != null && !String.IsNullOrEmpty(App.CurrentDisaster.EventName));

            await ReadXML();
            TP_OtherSettings os = this.FirstOrDefault();
            if (os == null) // file read failed
            {
                await GenerateDefaultOtherSettings();
                os = this.First();
            }
            // Distribute:
            App.CurrentOtherSettings = this.First();
            App.CurrentPatient.PatientID = "";
            if (!String.IsNullOrEmpty(App.OrgPolicy.OrgPatientIdPrefixText))
                App.CurrentPatient.PatientID = App.OrgPolicy.OrgPatientIdPrefixText;
            App.CurrentPatient.PatientID += os.CurrentNewPatientNumber;
            App.CurrentPatient.PatientID = App.OrgPolicy.ForceValidFormatID(App.CurrentPatient.PatientID);
            // to do: practice number

            // Find corresponding list item:
            foreach (var i in App.CurrentDisasterList)
            {
                if (i.EventName == os.CurrentEventName || i.EventShortName == os.CurrentEventShortName)
                {
                    App.CurrentDisaster = i;
                    break;
                }
            }
            if (App.CurrentDisaster == null || String.IsNullOrEmpty(App.CurrentDisaster.EventName)) // couldn't find match
            {
                App.CurrentDisaster = App.CurrentDisasterList.First();
            }
        }

        public async Task ReadXML()
        {
            await ReadXML(OTHER_SETTINGS_FILENAME); 
        }

        public async Task ReadXML(string filename)
        {
            LocalStorage.Data.Clear();
            await LocalStorage.Restore<TP_OtherSettings>(filename);
            if (LocalStorage.Data != null)
                foreach (var item in LocalStorage.Data)
                {
                    inner.Add(item as TP_OtherSettings); // if there's more than 1 we're going to ignore them.
                }
        }

        public async Task WriteXML()
        {
            await WriteXML(OTHER_SETTINGS_FILENAME);
        }

        public async Task WriteXML(string filename)
        {
            LocalStorage.Data.Clear();
            foreach (var item in inner)
                LocalStorage.Add(item as TP_OtherSettings);

            await LocalStorage.Save<TP_OtherSettings>(filename);
        }


    }
}