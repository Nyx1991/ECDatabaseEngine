using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ECDatabaseEngine
{
    internal abstract class ECSqlStatementBuilderBase
    {

        #region SQL Statements creation

        public string GenerateSqlForECTableWithPreparedStatements(ECTable _table,
                                                                  ref Dictionary<string, string> _parameters)
        {
            string sql = MakeSelectFrom(_table, true);

            sql += MakeJoins(_table);

            sql += CreateParameterizedWhereClause(_table, ref _parameters);

            sql += CreateOrderByClause(_table, true);

            return sql;
        }

        virtual protected string MakeSelectFrom(ECTable _table, bool _isRootTable = false)
        {
            string ret;
            if (_isRootTable)
                ret = "SELECT ";
            else
                ret = "";

            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                ret += GetSqlTableName(_table) + p.Name + " AS '" + _table.TableName + "." + p.Name + "',";
            }
            foreach (ECJoin j in _table.Joins)
            {
                ret += MakeSelectFrom((ECTable)j.Table) + ",";
            }
            ret = ret.Substring(0, ret.Length - 1);

            if (_isRootTable)
                ret += " FROM " + "`" + _table.TableName + "`";

            return ret;
        }

        virtual protected string CreateParameterizedWhereClause(ECTable _table, ref Dictionary<string, string> _parameters)
        {
            string sql = "";
            List<string> where = new List<string>();            

            CreateParameterizedWhereClause(_table, ref where, ref _parameters);
            
            if (where.Count != 0)
            {
                sql += " WHERE ";
                foreach (string s in where)
                    sql += "(" + s + ") AND";
                sql = sql.Substring(0, sql.Length - 4);
            }

            return sql;
        }

        virtual protected void CreateParameterizedWhereClause(ECTable _table, 
                                                             ref List<string> _where, 
                                                             ref Dictionary<string, string> _parameters)        
        {


            foreach (KeyValuePair<string, KeyValuePair<string, string>> kp in _table.Ranges)
                if (kp.Value.Value.Equals(""))
                {
                    string keyParm = _table.TableName + kp.Key;
                    _parameters.Add(keyParm, kp.Value.Key);
                    _where.Add(String.Format("{0}{1}=@{2}",
                            GetSqlTableName(_table),
                            kp.Key,
                            keyParm));                     
                }
                else
                {
                    string keyParm = _table.TableName + kp.Key;
                    _parameters.Add($"K{keyParm}", kp.Value.Key);
                    _parameters.Add($"V{keyParm}", kp.Value.Value);
                    _where.Add(String.Format("({0} BETWEEN @K{2} AND @V{3}",
                                            GetSqlTableName(_table), 
                                            kp.Key, 
                                            keyParm, 
                                            keyParm));
                }

            foreach (KeyValuePair<string, string> kp in _table.Filter)
            {
                _where.Add(ParseFilterString(_table, kp.Key, kp.Value, ref _parameters));
            }

            foreach (ECJoin j in _table.Joins)
            {
                ECTable joinTable = (ECTable)j.Table;
                CreateParameterizedWhereClause(joinTable, ref _where, ref _parameters);
            }

        }

        virtual protected string CreateOrderByClause(ECTable _table, bool _isRootTable = false)
        {
            string order = "";

            foreach (string s in _table.Order)
                order += GetSqlTableName(_table) + s + ",";

            foreach (ECJoin j in _table.Joins)
            {
                ECTable joinTable = (ECTable)j.Table;
                order += CreateOrderByClause(joinTable) + ",";
            }
            if (order.Length > 2)
            { 
                return " ORDER BY " + order.Substring(0, order.Length - 2) + " " + _table.OrderType.ToString();
            }
            else
            {
                return _isRootTable ? ";" : "";                
            }
        }

        virtual protected string MakeJoins(ECTable _table)
        {
            string ret = "";

            foreach (ECJoin j in _table.Joins)
            {
                ECTable joinTable = ((ECTable)j.Table);
                switch (j.JoinType)
                {
                    case ECJoinType.Inner:
                        ret += " INNER JOIN ";
                        break;
                    case ECJoinType.LeftOuter:
                        ret += " LEFT OUTER JOIN ";
                        break;
                    case ECJoinType.RightOuter:
                        ret += " RIGHT OUTER JOIN ";
                        break;
                }
                if (j.OnTargetField != null)
                    ret += "`" + joinTable.TableName + "` ON " + "`" + joinTable.TableName + "`." + j.OnTargetField + "=`" + _table.TableName + "`." + j.OnSourceField;
                else
                    ret += "`" + joinTable.TableName + "` ON " + "`" + joinTable.TableName + "`.RecId=`" + _table.TableName + "`." + j.OnSourceField;
                ret += MakeJoins(joinTable);
            }

            return ret;
        }

        virtual protected string ParseFilterString(ECTable _table, 
                                                  string _fieldName, 
                                                  string _filter, 
                                                  ref Dictionary<string, string> _parameter)
        {
            string fieldName = "`" + _fieldName + "`";
            string[] val = { "", "" };
            int valId = 0;
            bool foundPoint = false;
            string clause = "(" + fieldName;
            string operators = "<>=";
            for (int i = 0; i < _filter.Length; i++)
            {
                switch (_filter[i])
                {
                    case '<':
                        if (!foundPoint)
                            clause += '<';
                        else
                        {
                            clause += ProcessFromToOperator(_table, i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '>':
                        if (!foundPoint)
                            clause += '>';
                        else
                        {
                            clause += ProcessFromToOperator(_table, i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '=':
                        if (!foundPoint)
                            clause += '=';
                        else
                        {
                            clause += ProcessFromToOperator(_table, i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '|':
                        if (!foundPoint)
                        {
                            if (!operators.Contains(clause.Last()))
                                clause += "=";
                            clause += String.Format("@F{0}{1} OR {2}",
                                                    _table.TableName,
                                                    i,
                                                    fieldName);
                            _parameter.Add("F" + _table.TableName + i, val[valId % 2]);
                        }
                        else
                        {
                            clause += ProcessFromToOperator(_table, i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        val[valId % 2] = "";
                        break;
                    case '&':
                        if (!foundPoint)
                        {
                            if (!operators.Contains(clause.Last()))
                                clause += "=";
                            clause += String.Format("@F{0}{1} AND {2}",
                                                    _table.TableName,
                                                    i,
                                                    fieldName);
                            _parameter.Add($"F{i}", val[valId % 2]);
                        }
                        else
                        {
                            clause += ProcessFromToOperator(_table, i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        val[valId % 2] = "";
                        break;
                    case '.':
                        if (foundPoint) //found second . => switch to second value storage
                            valId++;
                        else //found first . => remember for next loop (we're now in another State)
                            foundPoint = true;
                        break;
                    default:
                        val[valId % 2] += _filter[i];
                        break;
                }
            }

            if (foundPoint) // we're at the end of the line and still havent processed the .'s. That Means we have sth. like "1..5" or "1.." or "..5"
            {
                clause += ProcessFromToOperator(_table, _filter.Length, val[valId % 2], val[(valId + 1) % 2],
                                                _filter[_filter.Length - 1], ref _parameter);
            }
            else
            {
                if (!operators.Contains(clause.Last()))
                    clause += "=";
                clause += "@F" + _table.TableName + _filter.Length;
                _parameter.Add("F" + _table.TableName + _filter.Length, val[valId % 2]);
            }

            return clause + ")";
        }

        virtual protected string ProcessFromToOperator(ECTable _table, int id, string currVal, string lastVal, char lastChar, ref Dictionary<string, string> _parameter)
        {
            string clause = "";

            if (lastVal == "" || currVal == "")
            {
                if (lastChar == '.') //case: "1.."
                {
                    clause += ">=";
                    _parameter.Add("F" + _table.TableName + id, lastVal);
                }
                else //case: "..5"
                {
                    clause += "<=";
                    _parameter.Add("F" + _table.TableName + id, currVal);
                }
                clause += "@F" + id;
            }
            else //case: "1..5"
            {
                clause += " BETWEEN ";
                clause += "@F" + _table.TableName + (id - 1);
                clause += " AND ";
                clause += "@F" + _table.TableName + id;

                _parameter.Add("F" + _table.TableName + (id - 1), lastVal);
                _parameter.Add("F" + _table.TableName + id, currVal);
            }

            return clause;
        }

        virtual protected string GetSqlTableName(ECTable _table)
        {
            return "`" + _table.TableName + "`.";
        }
        
        #endregion
    }
}
