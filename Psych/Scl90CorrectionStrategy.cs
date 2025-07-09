using System;
using System.Linq;
using PshchAPI.Controllers;
using PshchAPI.App_Data;
using PshchAPI.Models.SQLEntity;

namespace PshchAPI.Strategies
{
    /// <summary>
    /// SCL-90问卷的批改评分实现类，实现具体的评分算法
    /// </summary>
    public class Scl90CorrectionStrategy : ISurveyCorrectionStrategy
    {
        // SCL-90每个因子对应的题号（请根据实际问卷题号调整）
        private static readonly int[] Somatization = {1,4,12,27,40,42,48,49,52,53,56,58}; // 躯体化
        private static readonly int[] Obsession = {3,9,10,28,38,45,46,51,55,65}; // 强迫症状
        private static readonly int[] InterpersonalSensitivity = {6,21,34,36,37,41,61,69,73}; // 人际关系敏感
        private static readonly int[] Depression = {5,14,15,20,22,26,29,30,31,32,54,71,79}; // 忧郁
        private static readonly int[] Anxiety = {2,17,23,33,39,57,72,78,80,86}; // 焦虑
        private static readonly int[] Hostility = {11,24,63,67,74,81}; // 敌对
        private static readonly int[] Phobia = {13,25,47,50,70,75,82}; // 恐怖
        private static readonly int[] Paranoia = {8,18,43,68,76,83}; // 偏执
        private static readonly int[] Psychoticism = {7,16,35,62,77,84,85,87,88,90}; // 精神病性
        private static readonly int[] OtherItems = {19,44,59,60,66,77}; // 其他项目（部分题号可能与上面重叠，需根据实际问卷调整）

        /// <summary>
        /// 实现SCL-90的评分逻辑，统计每个因子的得分，并保存到数据库
        /// </summary>
        /// <param name="response">用户提交的答卷数据</param>
        /// <param name="db">数据库上下文</param>
        /// <returns>返回每个因子的得分</returns>
        public object Correct(SurveyController.ResponseDto response, PshchContext db)
        {
            // 1. 保存用户答卷到t_responses表
            foreach (var reply in response.QuestionReply)
            {
                var tResponse = new TResponse
                {
                    UserId = response.UserId,
                    SurveyId = response.SurveyID,
                    QuestionId = reply.QuestionId,
                    OptionId = reply.OptionValue, // 这里假设OptionValue即为选项ID，如有不同请调整
                    Content = reply.OptionContext
                };
                db.TResponses.Add(tResponse);
            }
            db.SaveChanges();

            // 2. 计算每个因子的得分（一般为均分，部分文献用总分）
            decimal somatization = AvgScore(response, Somatization);
            decimal obsession = AvgScore(response, Obsession);
            decimal interpersonalSensitivity = AvgScore(response, InterpersonalSensitivity);
            decimal depression = AvgScore(response, Depression);
            decimal anxiety = AvgScore(response, Anxiety);
            decimal hostility = AvgScore(response, Hostility);
            decimal phobia = AvgScore(response, Phobia);
            decimal paranoia = AvgScore(response, Paranoia);
            decimal psychoticism = AvgScore(response, Psychoticism);
            decimal otherItems = AvgScore(response, OtherItems);

            // 3. 计算总分、阴性项目数、阳性项目数、阳性项目均分
            var allScores = response.QuestionReply.Select(q => (decimal)q.OptionValue).ToList();
            decimal totalScore = Math.Round(allScores.Sum() / 90m, 2); // 总均分
            int yinScore = allScores.Count(s => s == 1); // 阴性项目数（无症状）
            int yangScore = allScores.Count(s => s > 1); // 阳性项目数（有症状）
            decimal yangAverageScore = yangScore > 0 ? Math.Round(allScores.Where(s => s > 1).Sum() / yangScore, 2) : 0; // 阳性项目均分

            // 4. 保存统计结果到t_scl90表
            var scl90 = new TScl90
            {
                UserId = response.UserId,
                TotalScore = totalScore,
                YinScore = yinScore,
                YangScore = yangScore,
                YangAverageScore = yangAverageScore,
                Somatization = somatization,
                Obsession = obsession,
                InterpersonalSensitivity = interpersonalSensitivity,
                Depression = depression,
                Anxiety = anxiety,
                Hostility = hostility,
                Phobia = phobia,
                Paranoia = paranoia,
                Psychoticism = psychoticism,
                OtherItems = otherItems,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            db.TScl90s.Add(scl90);
            db.SaveChanges();

            // 5. 返回各因子得分及总分、阴阳性项目数
            var result = new {
                totalScore, // 总均分
                yinScore,   // 阴性项目数
                yangScore,  // 阳性项目数
                yangAverageScore, // 阳性项目均分
                somatization, // 躯体化
                obsession,    // 强迫症状
                interpersonalSensitivity, // 人际关系敏感
                depression,   // 忧郁
                anxiety,      // 焦虑
                hostility,    // 敌对
                phobia,       // 恐怖
                paranoia,     // 偏执
                psychoticism, // 精神病性
                otherItems    // 其他项目
            };
            return result;
        }

        // 计算某一因子的均分
        private decimal AvgScore(SurveyController.ResponseDto response, int[] questionIds)
        {
            var scores = response.QuestionReply.Where(q => questionIds.Contains(q.QuestionId)).Select(q => (decimal)q.OptionValue).ToList();
            if (scores.Count == 0) return 0;
            return Math.Round(scores.Sum() / scores.Count, 2);
        }
    }
} 