-- Read-only CTE fixture; no Department entity/table is added.
-- Ranking ties use Score alone. Only ROW_NUMBER has a unique tie-breaker.
WITH Scores AS (
    SELECT * FROM (VALUES
        (1, N'A', 90, 10), (2, N'A', 90, 20),
        (3, N'A', 80, 30), (4, N'A', 70, 40),
        (5, N'B', 90, 5), (6, N'B', 80, 7)
    ) AS v(Id, Cohort, Score, Amount)
)
SELECT Id, Cohort, Score, Amount,
       ROW_NUMBER() OVER(PARTITION BY Cohort ORDER BY Score DESC, Id) AS RowNumber,
       RANK() OVER(PARTITION BY Cohort ORDER BY Score DESC) AS RankValue,
       DENSE_RANK() OVER(PARTITION BY Cohort ORDER BY Score DESC) AS DenseRankValue,
       SUM(CAST(Amount AS bigint)) OVER(PARTITION BY Cohort ORDER BY Score DESC, Id
           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RowsTotal,
       SUM(CAST(Amount AS bigint)) OVER(PARTITION BY Cohort ORDER BY Score DESC
           RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RangeTotal,
       LAG(Amount) OVER(PARTITION BY Cohort ORDER BY Score DESC, Id) AS PreviousAmount,
       LEAD(Amount) OVER(PARTITION BY Cohort ORDER BY Score DESC, Id) AS NextAmount
FROM Scores
ORDER BY Cohort, Score DESC, Id;
