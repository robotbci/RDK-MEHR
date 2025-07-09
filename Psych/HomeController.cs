using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PshchAPI.App_Data;
using PshchAPI.Models.SQLEntity;
using System.Security.Cryptography.X509Certificates;

namespace PshchAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
        private readonly PshchContext _context;
        private readonly EDContext _edContext;
        public HomeController(PshchContext context,EDContext edContext)
        {
            //依赖注入
            _context = context;
            _edContext = edContext;
        }

        [HttpPost("getEmotionKey")]
        public async Task<IActionResult> GetEmotionKey([FromForm] int userId)
        {
            try
            {
                var emotionKey = await _context.TEmotionkeys.FirstOrDefaultAsync(e => e.UserId == userId);
                if (emotionKey == null)
                {
                    //如果没有数据就创建一个
                    _context.TEmotionkeys.AddAsync(new TEmotionkey { UserId = userId });
                    await _context.SaveChangesAsync();//必须使用await

                    //return Unauthorized(new { Success = false, Message = "未检测到数据!" });
                    var emotionKey2 = await _context.TEmotionkeys.FirstOrDefaultAsync(e => e.UserId == userId);
                    Ok(new
                    {
                        Success = true,
                        Message = "暂时没有检测到您的数据!",
                        EmotionKeyData = emotionKey2
                    });
                }

                return Ok(new
                {
                    Success = true,
                    Message = "检测到数据!",
                    EmotionKeyData = emotionKey
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "系统错误，请稍后再试" });
            }
        }

        [HttpGet("showUsers")]
        public IActionResult ShowUsers()
        {
            var users = _context.TUsers.ToList();
            return Ok(users);
        }

        /// <summary>
        /// 返回用户SCL因子数据
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        [HttpPost("SCL90Factor")]
        public IActionResult SCL90Factor([FromForm]int userID=0)
        {
            //获取用户SCL近10因子和阴阳项目，总分记录的平均值
            // 验证用户ID是否有效
            if (userID==0)
            {
                return BadRequest(new { Success = false, Message = "用户ID不能为空" });
            }

            // 获取指定用户的SCL-90记录
            var userRecords = _context.TScl90s
                .Where(x => x.UserId == userID)
                .OrderByDescending(x => x.CreatedAt)//按照时间降序，新->旧
                .Take(10) // 只取最近10条记录
                .ToList();

            if (!userRecords.Any())
            {
                return NotFound(new { Success = false, Message = "未找到该用户的SCL-90记录" });
            }

            // 使用LINQ计算平均值，避免手动循环
            var factorsDto = new Scl90Dto
            {
                ItemId = userRecords.First().ItemId,
                UserId = userRecords.First().UserId,
                TotalScore = Math.Round(userRecords.Average(x => x.TotalScore), 1),
                YinScore = Math.Round(userRecords.Average(x => x.YinScore), 1),
                YangScore = Math.Round(userRecords.Average(x => x.YangScore), 1),
                YangAverageScore = Math.Round(userRecords.Average(x => x.YangAverageScore), 1),
                Somatization = Math.Round(userRecords.Average(x => x.Somatization), 1),
                Obsession = Math.Round(userRecords.Average(x => x.Obsession), 1),
                InterpersonalSensitivity = Math.Round(userRecords.Average(x => x.InterpersonalSensitivity), 1),
                Depression = Math.Round(userRecords.Average(x => x.Depression), 1),
                Anxiety = Math.Round(userRecords.Average(x => x.Anxiety), 1),
                Hostility = Math.Round(userRecords.Average(x => x.Hostility), 1),
                Phobia = Math.Round(userRecords.Average(x => x.Phobia), 1),
                Paranoia = Math.Round(userRecords.Average(x => x.Paranoia), 1),
                Psychoticism = Math.Round(userRecords.Average(x => x.Psychoticism), 1),
                OtherItems = Math.Round(userRecords.Average(x => x.OtherItems), 1),
                CreatedAt = userRecords.Max(x => x.CreatedAt),
                UpdatedAt = userRecords.Max(x => x.UpdatedAt)
            };

            return Ok(new
            {
                Success = true,
                Data = factorsDto,
                RecordCount = userRecords.Count,
                Message = "成功获取SCL-90因子分析结果"
            });
        }

        [HttpPost("SCL90Records")]
        public IActionResult SCL90Records([FromForm]int userID)
        {
            // 输入验证
            if (userID <= 0)
            {
                return BadRequest(new { Success = false, Message = "无效的用户ID" });
            }

            try
            {
                // 获取指定用户的所有SCL90测试记录
                var records = _context.TScl90s
                    .Where(x => x.UserId == userID)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new Scl90Dto
                    {
                        ItemId = x.ItemId,
                        TotalScore = x.TotalScore,
                        YinScore = x.YinScore,
                        YangScore = x.YangScore,
                        YangAverageScore = x.YangAverageScore,
                        Somatization = x.Somatization,
                        Obsession = x.Obsession,
                        InterpersonalSensitivity = x.InterpersonalSensitivity,
                        Depression = x.Depression,
                        Anxiety = x.Anxiety,
                        Hostility = x.Hostility,
                        Phobia = x.Phobia,
                        Paranoia = x.Paranoia,
                        Psychoticism = x.Psychoticism,
                        OtherItems = x.OtherItems,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList();

                if (!records.Any())
                {
                    return NotFound(new { Success = false, Message = "未找到该用户的SCL-90测试记录" });
                }

                return Ok(new
                {
                    Success = true,
                    Data = records,
                    Count = records.Count,
                    Message = "成功获取用户SCL-90测试记录"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = "服务器内部错误" });
            }
        }

        [HttpGet("EegProcessedData")]
        public async Task<IActionResult> EegProcessedData()
        {
            // 核心优化：异步查询 + AsNoTracking 提升性能
            // 1. 先获取最新的100条记录（按降序排列）
            var latest100Records = await _edContext.EegProcessedData
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt) // 先按降序获取最新的100条
                .Take(100)
                .ToListAsync();

            if (latest100Records == null)
            {
                return NotFound(new
                {
                    Success = false
                });
            }
            // 2. 对这100条记录进行二次排序（升序排列）
            var sortedRecords = latest100Records
                .OrderBy(x => x.CreatedAt) // 再按升序排列（从旧到新）
                .ToList();

            return Ok(new
            {
                Success = true,
                Data = sortedRecords
            });
        }
        // 使用record类型替代class，更适合DTO
        public record Scl90Dto
        {
            /// <summary>
            /// 主键ID
            /// </summary>
            public int ItemId { get; init; }

            /// <summary>
            /// 用户ID（关联t_User的itemID）
            /// </summary>
            public int UserId { get; init; }

            /// <summary>
            /// 总均分：总分÷90，表示总的来看，被试的自我感觉介于1－5的哪一个范围
            /// </summary>
            public decimal TotalScore { get; init; }

            /// <summary>
            /// 阴性项目数：表示被试"无症状"的项目有多少。
            /// </summary>
            public decimal YinScore { get; init; }

            /// <summary>
            /// 阳性项目数：表示被试在多少项目中呈现"有症状"
            /// </summary>
            public decimal YangScore { get; init; }

            /// <summary>
            /// 阳性项目均分：表示"有症状"项目的平均得分
            /// </summary>
            public decimal YangAverageScore { get; init; }

            /// <summary>
            /// 1.躯体化得分
            /// </summary>
            public decimal Somatization { get; init; }

            /// <summary>
            /// 2.强迫症状得分
            /// </summary>
            public decimal Obsession { get; init; }

            /// <summary>
            /// 3.人际关系敏感得分
            /// </summary>
            public decimal InterpersonalSensitivity { get; init; }

            /// <summary>
            /// 4.忧郁得分
            /// </summary>
            public decimal Depression { get; init; }

            /// <summary>
            /// 5.焦虑得分
            /// </summary>
            public decimal Anxiety { get; init; }

            /// <summary>
            /// 6.敌对得分
            /// </summary>
            public decimal Hostility { get; init; }

            /// <summary>
            /// 7.恐怖得分
            /// </summary>
            public decimal Phobia { get; init; }

            /// <summary>
            /// 8.偏执得分
            /// </summary>
            public decimal Paranoia { get; init; }

            /// <summary>
            /// 9.精神病性得分
            /// </summary>
            public decimal Psychoticism { get; init; }

            /// <summary>
            /// 10.其他项目得分
            /// </summary>
            public decimal OtherItems { get; init; }

            /// <summary>
            /// 测评时间
            /// </summary>
            public DateTime? CreatedAt { get; init; }

            /// <summary>
            /// 更新时间
            /// </summary>
            public DateTime? UpdatedAt { get; init; }
        }
    }
}
