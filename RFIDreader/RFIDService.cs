using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using RFIDLib;
using System.Threading;
using MySql.Data.MySqlClient;
using ServiceStack.Redis;
using Newtonsoft.Json;
using System.Configuration;
using System.Collections;

namespace RFIDreader
{
    public partial class RFIDService : ServiceBase
    {
        public static readonly string port = ConfigurationManager.AppSettings.Get("RFIDReader_port");
        TcpReader_5 m_rfidReader = new TcpReader_5();

        Dictionary<string, List<TAGDATA>> dictionary = new Dictionary<string, List<TAGDATA>>();
        ArrayList recordsList = new ArrayList();
        public RFIDService()
        {
            InitializeComponent();

            m_rfidReader.Start(int.Parse(port));

            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(TimedEvent);
            timer.Interval = 2000;// 5
            timer.Enabled = true;

        }

        private void TimedEvent(object sender, System.Timers.ElapsedEventArgs e)
        {
            List<TAGDATA> listTag = m_rfidReader.GetTagData();
            if (listTag.Count == 0)
            {
                return;
            }

            LogHelper.Log(LogLevel.Debug, "dictionary's count: " + dictionary.Count + ", get data size: " + listTag.Count);

            foreach (TAGDATA tag in listTag)
            {
                string id = tag.tagID.ToString();
                if (!dictionary.ContainsKey(id))
                {
                    dictionary.Add(id, new List<TAGDATA>());
                }
                List<TAGDATA> al = dictionary[id];
                al.Add(tag);
            }

            foreach (string key in dictionary.Keys)
            {
                List<TAGDATA> datas = dictionary[key];
                /*new Thread(new ThreadStart(()=> {
                    handleData(datas);
                })).Start();*/
                handleData(datas);
            }

            if (recordsList.Count > 0)
            {
                LogHelper.Log(LogLevel.Debug, "To save data: " + JsonConvert.SerializeObject(recordsList));
                int result = SaveRecords(recordsList);

                if (result > 0)
                {
                    recordsList.Clear();
                }
                else
                {
                    LogHelper.Log(LogLevel.Debug, "error! save data failed. " + JsonConvert.SerializeObject(recordsList));
                }
            }
        }

        public void handleData(List<TAGDATA> datas)
        {
            LogHelper.Log(LogLevel.Debug, "handle data: " + JsonConvert.SerializeObject(datas));
            int count = datas.Count;
            for (int i = 0; i < count; i++)
            {
                TAGDATA data = datas[i];
                DateTime dateTime = data.time;
                long seconds = (long)(DateTime.Now - dateTime).TotalSeconds;
                if (seconds > 300) // 5 minutes
                {
                    datas.Remove(data);
                    i--; count--;
                    LogHelper.Log(LogLevel.Debug, "lapsed data, removed." + JsonConvert.SerializeObject(data));
                    continue;
                }
                if (datas.Count == i + 1)
                {
                    continue;
                }
                TAGDATA next = datas[i + 1];
                if (next.readerID == data.readerID)
                {
                    datas.Remove(data);
                    LogHelper.Log(LogLevel.Debug, "replicate data, removed." + JsonConvert.SerializeObject(data));
                    i--; count--;
                }
                else
                {//  use next readerId as the in out type: 1 or 2
                    recordsList.Add(new { next.tagID, next.time, next.readerID });
                    datas.Remove(data);
                    datas.Remove(next);
                    LogHelper.Log(LogLevel.Debug, "access data, save to db." + JsonConvert.SerializeObject(next));
                    break;
                }
            }
        }

        public int SaveRecords(ArrayList list)
        {
            #region   插入单条数据

            string sql = @"insert into test.record(cardId, time, type) values (@tagID, @time, @readerID)";
            var result = DapperDBContext.Execute(sql, list); //直接传送list对象
            return result;
            #endregion
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
