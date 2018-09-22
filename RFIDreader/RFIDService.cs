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

        Dictionary<string,List<TAGDATA>> dictionary = new Dictionary<string, List<TAGDATA>>();
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
            LogHelper.Log(LogLevel.Debug, "dictionary's count: " + dictionary.Count + ", get data size: " + listTag.Count);
            foreach (TAGDATA tag in listTag){
                string id = tag.tagID.ToString();
                if (!dictionary.ContainsKey(id))
                {
                    dictionary.Add(id, new List<TAGDATA>());
                }
                List<TAGDATA> al = dictionary[id];
                al.Add(tag);

                //list.Add(new {tag.tagID, tag.time, tag.readerID, tag.strReaderIP, tag.type, tag.value1, tag.value2,  tag.bVoltLow});
            }
           
            foreach(string key in dictionary.Keys)
            {
                List<TAGDATA> datas = dictionary[key];
                /*new Thread(new ThreadStart(()=> {
                    handleData(datas);
                })).Start();*/
                handleData(datas);
            }
            if (recordsList.Count > 0)
            {
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
            //LogHelper.Log(LogLevel.Debug, JsonConvert.SerializeObject(list));
        }

        public void handleData(List<TAGDATA> datas)
        {
            LogHelper.Log(LogLevel.Debug, "handle data: " + JsonConvert.SerializeObject(datas));
            int count = datas.Count;
            for(int i=0; i<count;i++)
            {
                TAGDATA data = datas[i];
                DateTime dateTime =  data.time;
                long seconds = (long)(DateTime.Now - dateTime).TotalSeconds;
                if (seconds > 300) // 5 minutes
                {
                    datas.Remove(data);
                    i--;count--;
                    LogHelper.Log(LogLevel.Debug, "lapsed data, removed." + JsonConvert.SerializeObject(data));
                    continue;
                }
                int index = datas.IndexOf(data);
                if (datas.Count == index + 1)
                {
                    continue;
                }
                TAGDATA next = datas[index + 1];
                if(next.readerID == data.readerID){
                    datas.Remove(data);
                    LogHelper.Log(LogLevel.Debug, "replicate data, removed." + JsonConvert.SerializeObject(data));
                    i--;count--;
                }
                else
                {//  use next readerId as the in out type: 1 or 2
                    recordsList.Add(new { next.tagID, next.time, next.readerID });
                    LogHelper.Log(LogLevel.Debug, "access data, save to db." + JsonConvert.SerializeObject(next));
                }
            }
        }

        public int SaveRecords(ArrayList list)
        {
            #region   插入单条数据
            
            string sql = @"into test.record(cardId, time, type)values (@cardId, @time, @type)";
            var result = DapperDBContext.Execute(sql, list); //直接传送list对象
            return result;
            #endregion
        }

        List<uint> dataList = new List<uint>();

        int FindTag(TAGDATA tag)
        {
            for (int i = 0; i < dataList.Count; i++)
            {
                if (tag.tagID == dataList[i])
                    return i;
            }
            dataList.Add(tag.tagID);

            return -1;
        }


        public void Write2db(TAGDATA tag , String strType)
        { 
            String sql = "insert into test.rfid_data (tagId, time, readerId, readerIp, type, value1, value2) values (@tagId, @time, @readerId, @readerIp, @type, @value1, @value2)";

            MysqlManager<RfidData>.Execute(sql, new { tagId=tag.tagID, time=tag.time, readerId=tag.readerID,readerIp=tag.strReaderIP , type=tag.type, value1=tag.value1, value2=tag.value2 });

           
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
